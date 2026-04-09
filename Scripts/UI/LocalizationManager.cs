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
		{ "language", new Dictionary<Language, string> {
			{ Language.FR, "Langue" },
			{ Language.EN, "Language" },
			{ Language.ES, "Idioma" }
		}},
		{ "music", new Dictionary<Language, string> {
			{ Language.FR, "Musique" },
			{ Language.EN, "Music" },
			{ Language.ES, "Música" }
		}},
		{ "sfx", new Dictionary<Language, string> {
			{ Language.FR, "Effets sonores" },
			{ Language.EN, "Sound effects" },
			{ Language.ES, "Efectos sonoros" }
		}},
		{ "on", new Dictionary<Language, string> {
			{ Language.FR, "Activée" },
			{ Language.EN, "On" },
			{ Language.ES, "Activado" }
		}},
		{ "off", new Dictionary<Language, string> {
			{ Language.FR, "Désactivée" },
			{ Language.EN, "Off" },
			{ Language.ES, "Desactivado" }
		}},
		{ "close", new Dictionary<Language, string> {
			{ Language.FR, "Fermer" },
			{ Language.EN, "Close" },
			{ Language.ES, "Cerrar" }
		}},

		// Lobby
		{ "lobby_title", new Dictionary<Language, string> {
			{ Language.FR, "Multijoueur" },
			{ Language.EN, "Multiplayer" },
			{ Language.ES, "Multijugador" }
		}},
		{ "room_code", new Dictionary<Language, string> {
			{ Language.FR, "Code:" },
			{ Language.EN, "Code:" },
			{ Language.ES, "Código:" }
		}},
		{ "host", new Dictionary<Language, string> {
			{ Language.FR, "Créer" },
			{ Language.EN, "Create" },
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
		{ "room_created", new Dictionary<Language, string> {
			{ Language.FR, "Salon créé! Code:" },
			{ Language.EN, "Room created! Code:" },
			{ Language.ES, "Sala creada! Código:" }
		}},
		{ "error_start_server", new Dictionary<Language, string> {
			{ Language.FR, "Erreur: impossible de créer le salon" },
			{ Language.EN, "Error: unable to create room" },
			{ Language.ES, "Error: no se puede crear la sala" }
		}},
		{ "error_enter_code", new Dictionary<Language, string> {
			{ Language.FR, "Entrez un code à 6 caractères" },
			{ Language.EN, "Enter a 6 character code" },
			{ Language.ES, "Ingrese un código de 6 caracteres" }
		}},
		{ "searching_room", new Dictionary<Language, string> {
			{ Language.FR, "Recherche du salon" },
			{ Language.EN, "Searching for room" },
			{ Language.ES, "Buscando sala" }
		}},
		{ "room_not_found", new Dictionary<Language, string> {
			{ Language.FR, "Salon introuvable sur le réseau" },
			{ Language.EN, "Room not found on network" },
			{ Language.ES, "Sala no encontrada en la red" }
		}},
		{ "nickname", new Dictionary<Language, string> {
			{ Language.FR, "Pseudo :" },
			{ Language.EN, "Nickname:" },
			{ Language.ES, "Apodo:" }
		}},
		{ "guest_auth", new Dictionary<Language, string> {
			{ Language.FR, "Connexion invité" },
			{ Language.EN, "Guest login" },
			{ Language.ES, "Inicio invitado" }
		}},
		{ "save_nickname", new Dictionary<Language, string> {
			{ Language.FR, "Valider le pseudo" },
			{ Language.EN, "Save nickname" },
			{ Language.ES, "Guardar apodo" }
		}},
		{ "find_match", new Dictionary<Language, string> {
			{ Language.FR, "Trouver une partie" },
			{ Language.EN, "Find match" },
			{ Language.ES, "Buscar partida" }
		}},
		{ "authenticating", new Dictionary<Language, string> {
			{ Language.FR, "Connexion en cours..." },
			{ Language.EN, "Connecting..." },
			{ Language.ES, "Conectando..." }
		}},
		{ "guest_connected", new Dictionary<Language, string> {
			{ Language.FR, "Invité connecté" },
			{ Language.EN, "Guest connected" },
			{ Language.ES, "Invitado conectado" }
		}},
		{ "matchmaking_started", new Dictionary<Language, string> {
			{ Language.FR, "Recherche d'une partie..." },
			{ Language.EN, "Searching for a match..." },
			{ Language.ES, "Buscando partida..." }
		}},
		{ "match_found", new Dictionary<Language, string> {
			{ Language.FR, "Partie trouvée !" },
			{ Language.EN, "Match found!" },
			{ Language.ES, "¡Partida encontrada!" }
		}},
		{ "match_joined", new Dictionary<Language, string> {
			{ Language.FR, "Connexion au match..." },
			{ Language.EN, "Joining match..." },
			{ Language.ES, "Uniéndose a la partida..." }
		}},
		{ "username_taken", new Dictionary<Language, string> {
			{ Language.FR, "Pseudo déjà utilisé" },
			{ Language.EN, "Nickname already taken" },
			{ Language.ES, "Apodo ya usado" }
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
