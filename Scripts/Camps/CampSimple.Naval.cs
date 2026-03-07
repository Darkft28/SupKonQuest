using Godot;

public partial class CampSimple
{
	public void SetTileMapSol(TileMapLayer tileMapSol)
	{
		_tileMapSol = tileMapSol;
	}

	private void ProcessShipProductionQueue(double delta)
	{
		if (!HasPort) return;

		// Reseau : seul le peer qui possede ce camp traite la production navale
		if (!IsLocallyOwned()) return;

		if (_currentShipProduction == null && _shipProductionQueue.Count > 0)
		{
			_currentShipProduction = _shipProductionQueue.Dequeue();
			_shipProductionTimer = ShipStats.GetStats(_currentShipProduction).ProductionTime;
			GD.Print($"[Camp #{CampId}] Debut production navale: {_currentShipProduction} ({_shipProductionTimer}s)");
		}

		if (_currentShipProduction != null)
		{
			_shipProductionTimer -= (float)delta;

			if (_shipProductionTimer <= 0)
			{
				SpawnShip(_currentShipProduction);
				GD.Print($"[Camp #{CampId}] Production navale terminee: {_currentShipProduction}");
				_currentShipProduction = null;
			}
		}
	}

	public bool BuyShip(string shipType)
	{
		if (!HasPort) return false;

		var stats = ShipStats.GetStats(shipType);
		int price = stats.Price;
		int currentGold = GetGold();

		GD.Print($"[Camp #{CampId}] Tentative achat bateau {shipType} - Or: {currentGold}, Cout: {price}");

		int totalInQueue = _shipProductionQueue.Count + (_currentShipProduction != null ? 1 : 0);
		if (totalInQueue >= MaxShipQueueSize)
		{
			GD.Print($"File navale pleine ({MaxShipQueueSize} max)");
			return false;
		}

		if (currentGold < price)
		{
			GD.Print($"Pas assez d'or pour acheter {shipType} (cout: {price}, or: {currentGold})");
			return false;
		}

		if (IsNeutralCamp)
		{
			_localGold -= price;
		}
		else
		{
			if (GameManager.Instance == null) return false;
			if (!GameManager.Instance.SpendGold(TeamId, price)) return false;
		}

		_shipProductionQueue.Enqueue(shipType);
		GD.Print($"Bateau {shipType} ajoute a la file navale ({_shipProductionQueue.Count}/{MaxShipQueueSize}) - Cout: {price}");
		return true;
	}

	public bool CanBuyShip(string shipType)
	{
		if (!HasPort) return false;
		if (GameManager.Instance == null) return false;

		int totalInQueue = _shipProductionQueue.Count + (_currentShipProduction != null ? 1 : 0);
		if (totalInQueue >= MaxShipQueueSize) return false;

		var stats = ShipStats.GetStats(shipType);
		return GameManager.Instance.CanAfford(TeamId, stats.Price);
	}

	private void SpawnShip(string shipType)
	{
		var shipScene = GD.Load<PackedScene>("res://Scenes/Ship.tscn");
		if (shipScene == null)
		{
			GD.PrintErr("Impossible de charger Ship.tscn");
			return;
		}

		var ship = shipScene.Instantiate<Ship>();
		ship.ShipType = shipType;
		ship.TeamId = TeamId;

		// Positionner le bateau sur l'eau pres du port avec decalage
		Vector2 portGlobalPos = GetPortGlobalPosition();
		Vector2 spawnPos = FindWaterSpawnPosition(portGlobalPos);

		// Decaler les bateaux pour eviter l'empilement
		_spawnedShips.RemoveAll(s => s == null || !IsInstanceValid(s));
		if (_spawnedShips.Count > 0)
		{
			float angle = _spawnedShips.Count * Mathf.Tau / 6f;
			Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 100f;
			Vector2 offsetPos = spawnPos + offset;
			Vector2I offsetTile = _tileMapSol.LocalToMap(_tileMapSol.ToLocal(offsetPos));
			if (_tileMapSol != null && _tileMapSol.GetCellSourceId(offsetTile) == 6)
				spawnPos = offsetPos;
		}

		ship.GlobalPosition = spawnPos;
		if (_tileMapSol != null)
		{
			ship.SetTileMapSol(_tileMapSol);
		}

		// Reseau : assigner un NetworkId et broadcaster le spawn
		string networkId = NetworkEntityRegistry.GenerateId();
		ship.NetworkId = networkId;
		ship.IsLocalAuthority = true;

		GetParent().AddChild(ship);
		_spawnedShips.Add(ship);
		GD.Print($"[Camp #{CampId}] Bateau {shipType} spawne a {spawnPos}");

		// Envoyer le spawn aux autres peers
		NetworkSync.Instance?.SendSpawnShip(networkId, shipType, TeamId,
			ship.GlobalPosition.X, ship.GlobalPosition.Y, ship.GetCurrentHealth());
	}

	private Vector2 FindWaterSpawnPosition(Vector2 portPos)
	{
		if (_tileMapSol == null) return portPos;

		Vector2I portTile = _tileMapSol.LocalToMap(_tileMapSol.ToLocal(portPos));

		// Passe 1: chercher eau profonde (entouree d'eau) a partir de radius 2
		// pour eviter de spawner au bord de la cote
		for (int radius = 2; radius <= 8; radius++)
		{
			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				{
					if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue;

					Vector2I checkTile = portTile + new Vector2I(dx, dy);
					if (IsDeepWaterTile(checkTile))
					{
						return _tileMapSol.ToGlobal(_tileMapSol.MapToLocal(checkTile));
					}
				}
			}
		}

		// Passe 2: fallback sur simple tuile d'eau (radius 2+)
		for (int radius = 2; radius <= 8; radius++)
		{
			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				{
					if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue;

					Vector2I checkTile = portTile + new Vector2I(dx, dy);
					if (_tileMapSol.GetCellSourceId(checkTile) == 6)
					{
						return _tileMapSol.ToGlobal(_tileMapSol.MapToLocal(checkTile));
					}
				}
			}
		}

		return portPos;
	}

	private bool IsDeepWaterTile(Vector2I tile)
	{
		// Verifier que la tuile ET ses 8 voisins sont de l'eau
		for (int dx = -1; dx <= 1; dx++)
		{
			for (int dy = -1; dy <= 1; dy++)
			{
				if (_tileMapSol.GetCellSourceId(tile + new Vector2I(dx, dy)) != 6)
					return false;
			}
		}
		return true;
	}

	public Vector2 GetPortGlobalPosition()
	{
		if (_portSprite != null)
		{
			return _portSprite.GlobalPosition;
		}
		return GlobalPosition;
	}

	public int GetShipQueueCount()
	{
		return _shipProductionQueue.Count + (_currentShipProduction != null ? 1 : 0);
	}

	public int GetMaxShipQueueSize()
	{
		return MaxShipQueueSize;
	}

	public string GetCurrentShipProduction()
	{
		return _currentShipProduction;
	}

	public float GetShipProductionProgress()
	{
		if (_currentShipProduction == null) return 0f;
		float totalTime = ShipStats.GetStats(_currentShipProduction).ProductionTime;
		return 1f - (_shipProductionTimer / totalTime);
	}

	public string[] GetQueuedShips()
	{
		return _shipProductionQueue.ToArray();
	}

	public const int PortCost = 500;

	public bool CanBuyPort()
	{
		if (HasPort) return false;
		if (IsNeutralCamp) return false;
		if (GameManager.Instance == null) return false;
		return GameManager.Instance.CanAfford(TeamId, PortCost);
	}

	public bool BuyPort()
	{
		if (!CanBuyPort()) return false;
		if (!GameManager.Instance.SpendGold(TeamId, PortCost)) return false;
		GD.Print($"[PORT] Camp #{CampId} - Choisissez l'emplacement du port.");
		return true;
	}

	public bool PlacePortAt(Vector2 worldPos)
	{
		if (_tileMapSol == null) return false;

		Vector2I clickedTile = _tileMapSol.LocalToMap(_tileMapSol.ToLocal(worldPos));
		bool hasWater = false;
		for (int dx = -3; dx <= 3 && !hasWater; dx++)
			for (int dy = -3; dy <= 3 && !hasWater; dy++)
				if (_tileMapSol.GetCellSourceId(clickedTile + new Vector2I(dx, dy)) == 6)
					hasWater = true;

		if (!hasWater)
		{
			GD.Print("[PORT] Pas d'eau à proximité de cet emplacement.");
			return false;
		}

		HasPort = true;
		_portSprite = new Sprite2D();
		_portSprite.Texture = GD.Load<Texture2D>("res://Assets/Objects/Port.png");
		_portSprite.Scale = new Vector2(0.15f, 0.15f);
		AddChild(_portSprite);
		_portSprite.GlobalPosition = worldPos;

		GD.Print($"[PORT] Camp #{CampId} - Port placé à {worldPos}");
		return true;
	}

	public void TrySpawnPort(TileMapLayer tileMapSol)
	{
		_tileMapSol = tileMapSol;
		if (tileMapSol == null)
			return;

		const int TileSize = 128;
		const float CampScale = 4.5f;
		const float PortScale = 0.15f;
		const float LandOverlap = 0.2f; // 20% du port sur terre, 80% dans l'eau
		const float PortLongAxis = 1256f; // longueur en pixels des deux textures

		Vector2I campTile = tileMapSol.LocalToMap(GlobalPosition);

		// Directions cardinales : N, S, E, W
		Vector2I[] directions = new Vector2I[]
		{
			new Vector2I(0, -1), // Nord
			new Vector2I(0, 1),  // Sud
			new Vector2I(1, 0),  // Est
			new Vector2I(-1, 0), // Ouest
		};

		// 1) Trouver la meilleure direction (plus d'eau)
		int bestWaterCount = 0;
		int bestDirectionIndex = -1;

		for (int d = 0; d < directions.Length; d++)
		{
			int waterCount = 0;
			Vector2I dir = directions[d];

			for (int dist = 1; dist <= 8; dist++)
			{
				for (int offset = -2; offset <= 2; offset++)
				{
					Vector2I tilePos;
					if (dir.X == 0)
						tilePos = campTile + new Vector2I(offset, dir.Y * dist);
					else
						tilePos = campTile + new Vector2I(dir.X * dist, offset);

					if (tileMapSol.GetCellSourceId(tilePos) == 6)
						waterCount++;
				}
			}

			if (waterCount > bestWaterCount)
			{
				bestWaterCount = waterCount;
				bestDirectionIndex = d;
			}
		}

		if (bestWaterCount < 3 || bestDirectionIndex < 0)
			return;

		// 2) Trouver la distance de la première tuile d'eau (ligne centrale, offset=0)
		Vector2I bestDir = directions[bestDirectionIndex];
		int waterDist = -1;
		for (int dist = 1; dist <= 8; dist++)
		{
			Vector2I tilePos;
			if (bestDir.X == 0)
				tilePos = campTile + new Vector2I(0, bestDir.Y * dist);
			else
				tilePos = campTile + new Vector2I(bestDir.X * dist, 0);

			if (tileMapSol.GetCellSourceId(tilePos) == 6)
			{
				waterDist = dist;
				break;
			}
		}

		if (waterDist < 0)
			waterDist = 3;

		HasPort = true;
		_portSprite = new Sprite2D();

		// 3) Calculer la position du port
		// Côte = bord entre dernière tuile terre et première tuile eau
		// En monde : (waterDist - 0.5) * TileSize depuis le centre du camp
		// En local camp (scale 3) : diviser par CampScale
		float coastLocalDist = (waterDist - 0.5f) * TileSize / CampScale;

		// Demi-longueur du port en coordonnées locales du camp
		float halfLen = PortLongAxis * PortScale / 2f;
		// Décalage du centre vers l'eau pour avoir 20% terre / 80% eau
		float shift = (1f - 2f * LandOverlap) * halfLen;

		string texturePath;
		Vector2 portPosition;

		switch (bestDirectionIndex)
		{
			case 0: // Nord - eau vers Y négatif
				texturePath = "res://Assets/Objects/Port.png";
				_portSprite.Rotation = -Mathf.Pi / 2f;
				portPosition = new Vector2(0, -(coastLocalDist + shift));
				break;
			case 1: // Sud - eau vers Y positif
				texturePath = "res://Assets/Objects/Port.png";
				_portSprite.Rotation = Mathf.Pi / 2f;
				portPosition = new Vector2(0, coastLocalDist + shift);
				break;
			case 2: // Est - eau vers X positif
				texturePath = "res://Assets/Objects/Port.png";
				portPosition = new Vector2(coastLocalDist + shift, 0);
				break;
			case 3: // Ouest - eau vers X négatif
				texturePath = "res://Assets/Objects/Port.png";
				_portSprite.FlipH = true;
				portPosition = new Vector2(-(coastLocalDist + shift), 0);
				break;
			default:
				return;
		}

		_portSprite.Texture = GD.Load<Texture2D>(texturePath);
		_portSprite.Position = portPosition;
		_portSprite.Scale = new Vector2(PortScale, PortScale);
		AddChild(_portSprite);

		string[] dirNames = { "Nord", "Sud", "Est", "Ouest" };
		GD.Print($"[PORT] Camp #{CampId} - Port direction {dirNames[bestDirectionIndex]} (eau a {waterDist} tuiles, {bestWaterCount} tuiles d'eau)");
	}
}
