using Godot;
using System.Collections.Generic;

public partial class LocalizationManager : Node
{
	public static LocalizationManager Instance { get; private set; }

	public enum Language { FR, EN, ES }

	public Language CurrentLanguage { get; private set; } = Language.FR;

	[Signal]
	public delegate void LanguageChangedEventHandler();

	private Dictionary<string, Dictionary<Language, string>> _translations = new Dictionary<string, Dictionary<Language, string>>
	{
		// MainMenu
		{ "play", new Dictionary<Language, string> {
			{ Language.FR, "Jouer" },
			{ Language.EN, "Play" },
			{ Language.ES, "Jugar" }
		}},
		{ "multiplayer", new Dictionary<Language, string> {
			{ Language.FR, "Multijoueur" },
			{ Language.EN, "Multiplayer" },
			{ Language.ES, "Multijugador" }
		}},
		{ "options", new Dictionary<Language, string> {
			{ Language.FR, "Options" },
			{ Language.EN, "Options" },
			{ Language.ES, "Opciones" }
		}},
		{ "quit", new Dictionary<Language, string> {
			{ Language.FR, "Quitter" },
			{ Language.EN, "Quit" },
			{ Language.ES, "Salir" }
		}},

		// GameModeMenu
		{ "game_mode", new Dictionary<Language, string> {
			{ Language.FR, "Mode de jeu" },
			{ Language.EN, "Game Mode" },
			{ Language.ES, "Modo de juego" }
		}},
		{ "solo", new Dictionary<Language, string> {
			{ Language.FR, "Solo" },
			{ Language.EN, "Solo" },
			{ Language.ES, "Solo" }
		}},
		{ "multi", new Dictionary<Language, string> {
			{ Language.FR, "Multijoueur" },
			{ Language.EN, "Multiplayer" },
			{ Language.ES, "Multijugador" }
		}},
		{ "ia", new Dictionary<Language, string> {
			{ Language.FR, "Contre IA" },
			{ Language.EN, "VS AI" },
			{ Language.ES, "Contra IA" }
		}},
		{ "back", new Dictionary<Language, string> {
			{ Language.FR, "Retour" },
			{ Language.EN, "Back" },
			{ Language.ES, "Volver" }
		}},

		// Lobby
		{ "lobby_title", new Dictionary<Language, string> {
			{ Language.FR, "Multijoueur" },
			{ Language.EN, "Multiplayer" },
			{ Language.ES, "Multijugador" }
		}},
		{ "ip_address", new Dictionary<Language, string> {
			{ Language.FR, "Adresse IP:" },
			{ Language.EN, "IP Address:" },
			{ Language.ES, "Dirección IP:" }
		}},
		{ "port", new Dictionary<Language, string> {
			{ Language.FR, "Port:" },
			{ Language.EN, "Port:" },
			{ Language.ES, "Puerto:" }
		}},
		{ "host", new Dictionary<Language, string> {
			{ Language.FR, "Héberger" },
			{ Language.EN, "Host" },
			{ Language.ES, "Crear" }
		}},
		{ "join", new Dictionary<Language, string> {
			{ Language.FR, "Rejoindre" },
			{ Language.EN, "Join" },
			{ Language.ES, "Unirse" }
		}},
		{ "players_connected", new Dictionary<Language, string> {
			{ Language.FR, "Joueurs connectés:" },
			{ Language.EN, "Connected players:" },
			{ Language.ES, "Jugadores conectados:" }
		}},
		{ "start_game", new Dictionary<Language, string> {
			{ Language.FR, "Lancer la partie" },
			{ Language.EN, "Start Game" },
			{ Language.ES, "Iniciar partida" }
		}},
		{ "waiting", new Dictionary<Language, string> {
			{ Language.FR, "En attente..." },
			{ Language.EN, "Waiting..." },
			{ Language.ES, "Esperando..." }
		}},
		{ "server_started", new Dictionary<Language, string> {
			{ Language.FR, "Serveur démarré sur le port" },
			{ Language.EN, "Server started on port" },
			{ Language.ES, "Servidor iniciado en el puerto" }
		}},
		{ "error_start_server", new Dictionary<Language, string> {
			{ Language.FR, "Erreur: impossible de démarrer le serveur" },
			{ Language.EN, "Error: unable to start server" },
			{ Language.ES, "Error: no se puede iniciar el servidor" }
		}},
		{ "error_enter_ip", new Dictionary<Language, string> {
			{ Language.FR, "Erreur: entrez une adresse IP" },
			{ Language.EN, "Error: enter an IP address" },
			{ Language.ES, "Error: ingrese una dirección IP" }
		}},
		{ "connecting_to", new Dictionary<Language, string> {
			{ Language.FR, "Connexion à" },
			{ Language.EN, "Connecting to" },
			{ Language.ES, "Conectando a" }
		}},
		{ "error_connect", new Dictionary<Language, string> {
			{ Language.FR, "Erreur: impossible de se connecter" },
			{ Language.EN, "Error: unable to connect" },
			{ Language.ES, "Error: no se puede conectar" }
		}},
		{ "only_host_start", new Dictionary<Language, string> {
			{ Language.FR, "Seul l'hôte peut lancer la partie" },
			{ Language.EN, "Only the host can start the game" },
			{ Language.ES, "Solo el anfitrión puede iniciar la partida" }
		}},
		{ "starting_game", new Dictionary<Language, string> {
			{ Language.FR, "Lancement de la partie..." },
			{ Language.EN, "Starting game..." },
			{ Language.ES, "Iniciando partida..." }
		}},
		{ "player_connected", new Dictionary<Language, string> {
			{ Language.FR, "Joueur connecté" },
			{ Language.EN, "Player connected" },
			{ Language.ES, "Jugador conectado" }
		}},
		{ "player_disconnected", new Dictionary<Language, string> {
			{ Language.FR, "Joueur déconnecté" },
			{ Language.EN, "Player disconnected" },
			{ Language.ES, "Jugador desconectado" }
		}},
		{ "connected_to_server", new Dictionary<Language, string> {
			{ Language.FR, "Connecté au serveur!" },
			{ Language.EN, "Connected to server!" },
			{ Language.ES, "¡Conectado al servidor!" }
		}},
		{ "connection_failed", new Dictionary<Language, string> {
			{ Language.FR, "Échec de la connexion" },
			{ Language.EN, "Connection failed" },
			{ Language.ES, "Conexión fallida" }
		}},
		{ "disconnected_from_server", new Dictionary<Language, string> {
			{ Language.FR, "Déconnecté du serveur" },
			{ Language.EN, "Disconnected from server" },
			{ Language.ES, "Desconectado del servidor" }
		}},
		{ "host_suffix", new Dictionary<Language, string> {
			{ Language.FR, " (Hôte)" },
			{ Language.EN, " (Host)" },
			{ Language.ES, " (Anfitrión)" }
		}},
	};

	public override void _Ready()
	{
		if (Instance == null)
		{
			Instance = this;
		}
	}

	public string GetText(string key)
	{
		if (_translations.TryGetValue(key, out var langDict))
		{
			if (langDict.TryGetValue(CurrentLanguage, out var text))
			{
				return text;
			}
		}
		return key;
	}

	public void SetLanguage(Language lang)
	{
		CurrentLanguage = lang;
		EmitSignal(SignalName.LanguageChanged);
	}

	public void CycleLanguage()
	{
		CurrentLanguage = CurrentLanguage switch
		{
			Language.FR => Language.EN,
			Language.EN => Language.ES,
			Language.ES => Language.FR,
			_ => Language.FR
		};
		EmitSignal(SignalName.LanguageChanged);
	}

	public string GetLanguageCode()
	{
		return CurrentLanguage switch
		{
			Language.FR => "FR",
			Language.EN => "EN",
			Language.ES => "ES",
			_ => "FR"
		};
	}
}
