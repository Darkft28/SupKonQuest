using Godot;
using System;
using System.Collections.Generic;

public partial class TerritoryManager : Node2D
{
	private const int MapWidth = 256;
	private const int MapHeight = 256;
	private const int HalfWidth = MapWidth / 2;
	private const int HalfHeight = MapHeight / 2;
	private const int TileSize = 128;

	// Rayon du territoire autour d'un camp (en tuiles)
	private const int DefaultTerritoryRadius = 8;

	// teamId par tuile, -1 = wilderness
	private int[,] _territoryMap = new int[MapWidth, MapHeight];

	// Mode placement port
	private CampSimple _pendingPortCamp = null;
	public bool IsPortPlacementMode => _pendingPortCamp != null;

	public static TerritoryManager Instance { get; private set; }

	private TileMapLayer _solLayer;
	private Image _tintImage;
	private ImageTexture _tintTexture;
	private Sprite2D _tintSprite;

	private static readonly Color[] TeamColors = new Color[]
	{
		new Color(1, 0, 0, 1f),        // Rouge
		new Color(0, 0.5f, 1, 1f),     // Bleu
		new Color(0, 0.8f, 0, 1f),     // Vert
		new Color(1, 1, 0, 1f),        // Jaune
		new Color(1, 0, 1, 1f),        // Magenta
		new Color(0, 1, 1, 1f),        // Cyan
		new Color(1, 0.5f, 0, 1f),     // Orange
		new Color(0.5f, 0, 1, 1f),     // Violet
		new Color(0.6f, 0.3f, 0, 1f),  // Marron
		new Color(1, 0.4f, 0.7f, 1f),  // Rose
		new Color(0, 0.5f, 0, 1f),     // Vert foncé
		new Color(0.3f, 0.3f, 1, 1f),  // Bleu moyen
		new Color(1, 0.8f, 0, 1f),     // Or
		new Color(0, 0.8f, 0.6f, 1f),  // Turquoise
		new Color(0.8f, 0, 0.4f, 1f),  // Cramoisi
		new Color(0.5f, 0.8f, 0, 1f),  // Chartreuse
		new Color(1, 0.6f, 0.4f, 1f),  // Saumon
		new Color(0.4f, 0, 0.6f, 1f),  // Indigo
		new Color(0, 0.4f, 0.4f, 1f),  // Sarcelle
		new Color(0.8f, 0.8f, 0, 1f),  // Olive
		new Color(0.9f, 0.2f, 0.5f, 1f), // Framboise
		new Color(0.2f, 0.6f, 1, 1f),  // Azur
		new Color(0.7f, 1, 0.3f, 1f),  // Lime
		new Color(1, 0.3f, 0.3f, 1f),  // Corail
		new Color(0.6f, 0.4f, 1, 1f),  // Lavande
		new Color(0, 1, 0.5f, 1f),     // Menthe
		new Color(1, 0.5f, 0.5f, 1f),  // Pêche
		new Color(0.3f, 0, 0.3f, 1f),  // Prune
		new Color(0.4f, 0.7f, 0.4f, 1f), // Sauge
		new Color(0.9f, 0.6f, 0, 1f),  // Ambre
		new Color(0.5f, 0.5f, 1, 1f),  // Pervenche
		new Color(0.8f, 0.5f, 0.2f, 1f), // Cuivre
		new Color(0, 0.6f, 0.3f, 1f),  // Émeraude
		new Color(0.9f, 0, 0.9f, 1f),  // Fuchsia
		new Color(0.4f, 0.8f, 0.8f, 1f), // Aigue-marine
		new Color(0.7f, 0.2f, 0, 1f),  // Rouille
		new Color(0.5f, 1, 0.5f, 1f),  // Vert pâle
		new Color(0.3f, 0.5f, 0.7f, 1f), // Acier
		new Color(1, 0.9f, 0.4f, 1f),  // Crème
		new Color(0.6f, 0, 0.2f, 1f),  // Bordeaux
		new Color(0.2f, 0.8f, 0.4f, 1f), // Jade
		new Color(0.8f, 0.4f, 0.6f, 1f), // Mauve
	};

	private const float TintAlpha = 0.15f;
	private const float BorderAlpha = 0.5f;
	private const float BorderWidth = 4f;

	private int[,] _territoryGrid;

	public void SetSolLayer(TileMapLayer solLayer)
	{
		_solLayer = solLayer;
	}

	/// <summary>
	/// Retourne l'équipe propriétaire d'une tuile à une position monde donnée.
	/// Retourne -1 si la tuile n'appartient à aucune équipe (wilderness ou eau).
	/// </summary>
	public int GetTeamAtWorldPos(Vector2 worldPos)
	{
		if (_solLayer == null) return -1;
		Vector2I tile = _solLayer.LocalToMap(_solLayer.ToLocal(worldPos));
		int tx = tile.X + HalfWidth;
		int ty = tile.Y + HalfHeight;
		if (tx < 0 || tx >= MapWidth || ty < 0 || ty >= MapHeight) return -1;
		return _territoryMap[tx, ty];
	}

	/// <summary>
	/// Tuiles côtières du territoire d'une équipe (terre adjacente à l'eau), triées par qualité d'accès maritime.
	/// Utilisé par l'IA pour placer un port n'importe où sur son territoire, pas seulement près d'un camp.
	/// </summary>
	public IEnumerable<Vector2> EnumerateShorelinePositionsForTeam(int teamId)
	{
		if (_solLayer == null || teamId <= 0)
			yield break;

		Vector2I[] directions = { new Vector2I(0, -1), new Vector2I(0, 1), new Vector2I(1, 0), new Vector2I(-1, 0) };
		var scored = new List<(Vector2 worldPos, int score)>();

		for (int tx = 0; tx < MapWidth; tx++)
		{
			for (int ty = 0; ty < MapHeight; ty++)
			{
				if (_territoryMap[tx, ty] != teamId)
					continue;

				Vector2I tile = new Vector2I(tx - HalfWidth, ty - HalfHeight);
				if (_solLayer.GetCellSourceId(tile) == 6)
					continue;

				bool hasAdjacentWater = false;
				int waterScore = 0;
				foreach (var dir in directions)
				{
					if (_solLayer.GetCellSourceId(tile + dir) != 6)
						continue;

					hasAdjacentWater = true;
					for (int dist = 1; dist <= 8; dist++)
						for (int offset = -2; offset <= 2; offset++)
						{
							Vector2I tilePos = dir.X == 0
								? tile + new Vector2I(offset, dir.Y * dist)
								: tile + new Vector2I(dir.X * dist, offset);
							if (_solLayer.GetCellSourceId(tilePos) == 6)
								waterScore++;
						}
				}

				if (!hasAdjacentWater || waterScore < 1)
					continue;

				Vector2 worldPos = _solLayer.ToGlobal(_solLayer.MapToLocal(tile));
				scored.Add((worldPos, waterScore));
			}
		}

		scored.Sort((a, b) => b.score.CompareTo(a.score));
		foreach (var entry in scored)
			yield return entry.worldPos;
	}

	public void SetTerritoryGrid(int[,] grid)
	{
		_territoryGrid = grid;
	}

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
	}

	public void StartPortPlacement(CampSimple camp)
	{
		_pendingPortCamp = camp;
		GD.Print("[PORT] Click on the map to place the port.");
	}

	public void CancelPortPlacement()
	{
		if (_pendingPortCamp != null)
		{
			GameManager.Instance?.AddGold(_pendingPortCamp.GetTeamId(), CampSimple.PortCost);
			GD.Print("[PORT] Placement cancelled, gold refunded.");
		}
		_pendingPortCamp = null;
	}

	public void Initialize()
	{
		Instance = this;

		_tintSprite = new Sprite2D();
		_tintSprite.Centered = false;
		_tintSprite.Position = new Vector2(-HalfWidth * TileSize, -HalfHeight * TileSize);
		_tintSprite.Scale = new Vector2(TileSize, TileSize);
		_tintSprite.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
		AddChild(_tintSprite);

		ConnectCampSignals();
		ComputeTerritory();
		UpdateTintImage();
		QueueRedraw();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		Vector2 worldPos = GetGlobalMousePosition();

		// Mode placement port (prioritaire)
		if (_pendingPortCamp != null)
		{
			if (@event is InputEventMouseButton mb && mb.Pressed)
			{
				if (mb.ButtonIndex == MouseButton.Left)
				{
					var gameState = GetNodeOrNull<GameState>("/root/GameState");
					int localTeamId = gameState?.LocalTeamId ?? 1;

					// Same conversion as CampSimple.PlacePortAt to avoid land/water offsets.
					Vector2I clickedTile = _solLayer != null
						? _solLayer.LocalToMap(_solLayer.ToLocal(worldPos))
						: new Vector2I(Mathf.RoundToInt(worldPos.X / TileSize), Mathf.RoundToInt(worldPos.Y / TileSize));

					int tx = clickedTile.X + HalfWidth;
					int ty = clickedTile.Y + HalfHeight;
					bool isWaterTile = _solLayer != null && _solLayer.GetCellSourceId(clickedTile) == 6;
					bool tileOwned = tx >= 0 && tx < MapWidth && ty >= 0 && ty < MapHeight
						&& _territoryMap[tx, ty] == localTeamId;

					if (isWaterTile)
						GD.Print("[PORT] Click a territory tile (land) adjacent to water.");
					else if (!tileOwned)
						GD.Print("[PORT] This tile is not owned by your team.");
					else if (!_pendingPortCamp.PlacePortAt(worldPos))
						GD.Print("[PORT] No water here - choose a spot near water.");
					else
						_pendingPortCamp = null;
					GetViewport().SetInputAsHandled();
				}
				else if (mb.ButtonIndex == MouseButton.Right)
				{
					CancelPortPlacement();
					GetViewport().SetInputAsHandled();
				}
			}
			return;
		}
	}

	private void ConnectCampSignals()
	{
		// Signals are kept for potential listeners,
		// but territory is refreshed via direct calls from CaptureCamp().
		int connected = 0;
		foreach (Node node in GetTree().GetNodesInGroup("camps"))
		{
			if (node is CampSimple camp)
			{
				connected++;
			}
		}
		GD.Print($"[TERRITOIRE] ConnectCampSignals: {connected} camp(s) in group 'camps'.");
	}

	private Color GetTeamColor(int teamId)
	{
		if (teamId <= 0)
			return new Color(0.5f, 0.5f, 0.5f, 1f); // Gris pour neutre/invalide
		int index = (teamId - 1) % TeamColors.Length;
		return TeamColors[index];
	}

	private void ComputeTerritory()
	{
		for (int x = 0; x < MapWidth; x++)
			for (int y = 0; y < MapHeight; y++)
				_territoryMap[x, y] = -1;

		var camps = new List<(Vector2 pos, int teamId)>();
		foreach (Node node in GetTree().GetNodesInGroup("camps"))
		{
			if (node is CampSimple camp)
				camps.Add((camp.GlobalPosition, camp.TeamId));
		}

		if (camps.Count == 0)
			return;

		foreach (var (campPos, teamId) in camps)
		{
			int centerX = Mathf.RoundToInt(campPos.X / TileSize) + HalfWidth;
			int centerY = Mathf.RoundToInt(campPos.Y / TileSize) + HalfHeight;
			int radius = DefaultTerritoryRadius;
			int radiusSq = radius * radius;

			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				{
					if (dx * dx + dy * dy > radiusSq)
						continue;

					int tx = centerX + dx;
					int ty = centerY + dy;

					if (tx < 0 || tx >= MapWidth || ty < 0 || ty >= MapHeight)
						continue;

					Vector2I tileCoords = new Vector2I(tx - HalfWidth, ty - HalfHeight);
					if (_solLayer != null && _solLayer.GetCellSourceId(tileCoords) == 6)
						continue;

					// si déjà pris, le camp le plus proche gagne
					if (_territoryMap[tx, ty] >= 0 && _territoryMap[tx, ty] != teamId)
					{
						float worldX = (tx - HalfWidth) * TileSize + TileSize / 2f;
						float worldY = (ty - HalfHeight) * TileSize + TileSize / 2f;
						Vector2 tilePos = new Vector2(worldX, worldY);

						float existingDist = float.MaxValue;
						foreach (var (otherPos, otherId) in camps)
						{
							if (otherId == _territoryMap[tx, ty])
							{
								existingDist = tilePos.DistanceSquaredTo(otherPos);
								break;
							}
						}

						if (tilePos.DistanceSquaredTo(campPos) >= existingDist)
							continue;
					}

					_territoryMap[tx, ty] = teamId;
				}
			}
		}

		ApplyRegionConquest();
	}

	// Si une équipe contrôle tous les camps d'une région, toutes les tuiles de cette région lui appartiennent.
	private void ApplyRegionConquest()
	{
		if (_territoryGrid == null) return;

		var gameManager = GameManager.Instance;
		if (gameManager == null) return;

		var allCamps = gameManager.GetAllCamps();
		if (allCamps == null || allCamps.Count == 0) return;

		// Regrouper les camps par région
		var campsByRegion = new Dictionary<int, List<CampSimple>>();
		foreach (var camp in allCamps)
		{
			int r = camp.RegionId;
			if (r <= 0) continue;
			if (!campsByRegion.ContainsKey(r))
				campsByRegion[r] = new List<CampSimple>();
			campsByRegion[r].Add(camp);
		}

		// Pour chaque région, vérifier si une seule équipe possède tous les camps
		var conqueredRegions = new Dictionary<int, int>(); // regionId → teamId
		foreach (var (regionId, camps) in campsByRegion)
		{
			if (camps.Count == 0) continue;
			int firstTeam = camps[0].GetTeamId();
			if (firstTeam <= 0) continue;
			bool allSameTeam = true;
			foreach (var camp in camps)
			{
				if (camp.GetTeamId() != firstTeam) { allSameTeam = false; break; }
			}
			if (allSameTeam)
				conqueredRegions[regionId] = firstTeam;
		}

		if (conqueredRegions.Count == 0) return;

		// Remplir toutes les tuiles de la région conquise (hors eau)
		int gridW = _territoryGrid.GetLength(0);
		int gridH = _territoryGrid.GetLength(1);

		for (int x = 0; x < MapWidth; x++)
		{
			for (int y = 0; y < MapHeight; y++)
			{
				int gx = x < gridW ? x : -1;
				int gy = y < gridH ? y : -1;
				if (gx < 0 || gy < 0) continue;

				int regionId = _territoryGrid[gx, gy];
				if (!conqueredRegions.TryGetValue(regionId, out int teamId)) continue;

				Vector2I tileCoords = new Vector2I(x - HalfWidth, y - HalfHeight);
				if (_solLayer != null && _solLayer.GetCellSourceId(tileCoords) == 6) continue;

				_territoryMap[x, y] = teamId;
			}
		}
	}

	private void UpdateTintImage()
	{
		_tintImage = Image.CreateEmpty(MapWidth, MapHeight, false, Image.Format.Rgba8);

		for (int x = 0; x < MapWidth; x++)
		{
			for (int y = 0; y < MapHeight; y++)
			{
				int teamId = _territoryMap[x, y];
				if (teamId < 0)
				{
					_tintImage.SetPixel(x, y, new Color(0, 0, 0, 0));
				}
				else
				{
					Color teamColor = GetTeamColor(teamId);
					_tintImage.SetPixel(x, y, new Color(teamColor.R, teamColor.G, teamColor.B, TintAlpha));
				}
			}
		}

		_tintTexture = ImageTexture.CreateFromImage(_tintImage);
		_tintSprite.Texture = _tintTexture;
	}

	public override void _Draw()
	{
		for (int x = 0; x < MapWidth; x++)
		{
			for (int y = 0; y < MapHeight; y++)
			{
				int currentTeam = _territoryMap[x, y];
				if (currentTeam < 0)
					continue;

				Color teamColor = GetTeamColor(currentTeam);
				var borderColor = new Color(teamColor.R, teamColor.G, teamColor.B, BorderAlpha);

				int rightTeam = x + 1 < MapWidth ? _territoryMap[x + 1, y] : -1;
				if (rightTeam != currentTeam)
				{
					float px = (x + 1 - HalfWidth) * TileSize;
					float py1 = (y - HalfHeight) * TileSize;
					float py2 = (y + 1 - HalfHeight) * TileSize;
					DrawLine(new Vector2(px, py1), new Vector2(px, py2), borderColor, BorderWidth);
				}

				int bottomTeam = y + 1 < MapHeight ? _territoryMap[x, y + 1] : -1;
				if (bottomTeam != currentTeam)
				{
					float px1 = (x - HalfWidth) * TileSize;
					float px2 = (x + 1 - HalfWidth) * TileSize;
					float py = (y + 1 - HalfHeight) * TileSize;
					DrawLine(new Vector2(px1, py), new Vector2(px2, py), borderColor, BorderWidth);
				}

				int leftTeam = x - 1 >= 0 ? _territoryMap[x - 1, y] : -1;
				if (leftTeam != currentTeam)
				{
					float px = (x - HalfWidth) * TileSize;
					float py1 = (y - HalfHeight) * TileSize;
					float py2 = (y + 1 - HalfHeight) * TileSize;
					DrawLine(new Vector2(px, py1), new Vector2(px, py2), borderColor, BorderWidth);
				}

				int topTeam = y - 1 >= 0 ? _territoryMap[x, y - 1] : -1;
				if (topTeam != currentTeam)
				{
					float px1 = (x - HalfWidth) * TileSize;
					float px2 = (x + 1 - HalfWidth) * TileSize;
					float py = (y - HalfHeight) * TileSize;
					DrawLine(new Vector2(px1, py), new Vector2(px2, py), borderColor, BorderWidth);
				}
			}
		}
	}

	// Appelé directement depuis CampSimple.CaptureCamp() et ApplyRemoteCapture()
	public void RefreshTerritory(int captorTeamId = -1)
	{
		GD.Print($"[TERRITOIRE] RefreshTerritory() - captor: team {captorTeamId}");
		ComputeTerritory();
		UpdateTintImage();
		QueueRedraw();
	}
}
