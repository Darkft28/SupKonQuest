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
		{ "waiting_players", new Dictionary<Language, string> {
			{ Language.FR, "En attente de joueurs" },
			{ Language.EN, "Waiting for players" },
			{ Language.ES, "Esperando jugadores" }
		}},
		{ "starting_in", new Dictionary<Language, string> {
			{ Language.FR, "Démarrage dans" },
			{ Language.EN, "Starting in" },
			{ Language.ES, "Inicio en" }
		}},
		{ "starting_soon", new Dictionary<Language, string> {
			{ Language.FR, "Démarrage..." },
			{ Language.EN, "Starting..." },
			{ Language.ES, "Iniciando..." }
		}},
		{ "waiting_server", new Dictionary<Language, string> {
			{ Language.FR, "En attente du serveur..." },
			{ Language.EN, "Waiting for server..." },
			{ Language.ES, "Esperando al servidor..." }
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

		// Auth
		{ "auth_title", new Dictionary<Language, string> {
			{ Language.FR, "Compte SupKonQuest" },
			{ Language.EN, "SupKonQuest Account" },
			{ Language.ES, "Cuenta SupKonQuest" }
		}},
		{ "auth_login_tab", new Dictionary<Language, string> {
			{ Language.FR, "Connexion" },
			{ Language.EN, "Login" },
			{ Language.ES, "Iniciar sesión" }
		}},
		{ "auth_register_tab", new Dictionary<Language, string> {
			{ Language.FR, "Créer un compte" },
			{ Language.EN, "Register" },
			{ Language.ES, "Registrarse" }
		}},
		{ "auth_login", new Dictionary<Language, string> {
			{ Language.FR, "Se connecter" },
			{ Language.EN, "Log in" },
			{ Language.ES, "Iniciar sesión" }
		}},
		{ "auth_register", new Dictionary<Language, string> {
			{ Language.FR, "Créer le compte" },
			{ Language.EN, "Create account" },
			{ Language.ES, "Crear cuenta" }
		}},
		{ "auth_email", new Dictionary<Language, string> {
			{ Language.FR, "Email" },
			{ Language.EN, "Email" },
			{ Language.ES, "Correo" }
		}},
		{ "auth_username", new Dictionary<Language, string> {
			{ Language.FR, "Pseudo" },
			{ Language.EN, "Username" },
			{ Language.ES, "Usuario" }
		}},
		{ "auth_password", new Dictionary<Language, string> {
			{ Language.FR, "Mot de passe" },
			{ Language.EN, "Password" },
			{ Language.ES, "Contraseña" }
		}},
		{ "auth_confirm_password", new Dictionary<Language, string> {
			{ Language.FR, "Confirmer le mot de passe" },
			{ Language.EN, "Confirm password" },
			{ Language.ES, "Confirmar contraseña" }
		}},
		{ "auth_guest", new Dictionary<Language, string> {
			{ Language.FR, "Jouer en invité" },
			{ Language.EN, "Play as guest" },
			{ Language.ES, "Jugar como invitado" }
		}},
		{ "auth_restoring", new Dictionary<Language, string> {
			{ Language.FR, "Restauration de la session..." },
			{ Language.EN, "Restoring session..." },
			{ Language.ES, "Restaurando sesión..." }
		}},
		{ "auth_session_expired", new Dictionary<Language, string> {
			{ Language.FR, "Session expirée. Reconnectez-vous." },
			{ Language.EN, "Session expired. Please sign in again." },
			{ Language.ES, "Sesión expirada. Vuelve a iniciar sesión." }
		}},
		{ "auth_not_authenticated", new Dictionary<Language, string> {
			{ Language.FR, "Connexion requise." },
			{ Language.EN, "Sign-in required." },
			{ Language.ES, "Se requiere iniciar sesión." }
		}},
		{ "auth_error_invalid_credentials", new Dictionary<Language, string> {
			{ Language.FR, "Email ou mot de passe incorrect." },
			{ Language.EN, "Invalid email or password." },
			{ Language.ES, "Correo o contraseña incorrectos." }
		}},
		{ "auth_error_username_taken", new Dictionary<Language, string> {
			{ Language.FR, "Ce pseudo est déjà pris." },
			{ Language.EN, "This username is already taken." },
			{ Language.ES, "Este usuario ya existe." }
		}},
		{ "auth_error_username", new Dictionary<Language, string> {
			{ Language.FR, "Pseudo invalide (3-16 caractères, lettres/chiffres/_/-)." },
			{ Language.EN, "Invalid username (3-16 chars, letters/digits/_/-)." },
			{ Language.ES, "Usuario inválido (3-16 caracteres, letras/números/_/-)." }
		}},
		{ "auth_error_email", new Dictionary<Language, string> {
			{ Language.FR, "Adresse email invalide." },
			{ Language.EN, "Invalid email address." },
			{ Language.ES, "Correo electrónico inválido." }
		}},
		{ "auth_error_password", new Dictionary<Language, string> {
			{ Language.FR, "Mot de passe trop court (8 caractères minimum)." },
			{ Language.EN, "Password too short (minimum 8 characters)." },
			{ Language.ES, "Contraseña demasiado corta (mínimo 8 caracteres)." }
		}},
		{ "auth_error_password_mismatch", new Dictionary<Language, string> {
			{ Language.FR, "Les mots de passe ne correspondent pas." },
			{ Language.EN, "Passwords do not match." },
			{ Language.ES, "Las contraseñas no coinciden." }
		}},
		{ "auth_error_guest_only", new Dictionary<Language, string> {
			{ Language.FR, "Le pseudo invité ne s'applique qu'aux comptes invité." },
			{ Language.EN, "Guest nickname only applies to guest accounts." },
			{ Language.ES, "El apodo de invitado solo aplica a cuentas invitado." }
		}},
		{ "auth_error_network", new Dictionary<Language, string> {
			{ Language.FR, "Impossible de joindre le serveur Nakama. Vérifiez qu'il est démarré (project.godot → nakama/host)." },
			{ Language.EN, "Cannot reach the Nakama server. Check that it is running (project.godot → nakama/host)." },
			{ Language.ES, "No se puede conectar al servidor Nakama. Comprueba que esté en marcha (project.godot → nakama/host)." }
		}},
		{ "auth_logout", new Dictionary<Language, string> {
			{ Language.FR, "Déconnexion" },
			{ Language.EN, "Log out" },
			{ Language.ES, "Cerrar sesión" }
		}},

		{ "defeat", new Dictionary<Language, string> {
			{ Language.FR, "Défaite" },
			{ Language.EN, "Defeat" },
			{ Language.ES, "Derrota" }
		}},
		{ "victory", new Dictionary<Language, string> {
			{ Language.FR, "VICTOIRE !" },
			{ Language.EN, "VICTORY!" },
			{ Language.ES, "¡VICTORIA!" }
		}},
		{ "main_menu", new Dictionary<Language, string> {
			{ Language.FR, "Menu principal" },
			{ Language.EN, "Main menu" },
			{ Language.ES, "Menú principal" }
		}},

		// Classement en jeu
		{ "ranking_title", new Dictionary<Language, string> {
			{ Language.FR, "⚔  Classement" },
			{ Language.EN, "⚔  Ranking" },
			{ Language.ES, "⚔  Clasificación" }
		}},
		{ "ranking_camps", new Dictionary<Language, string> {
			{ Language.FR, "camp" },
			{ Language.EN, "camp" },
			{ Language.ES, "campo" }
		}},
		{ "ranking_camps_plural", new Dictionary<Language, string> {
			{ Language.FR, "camps" },
			{ Language.EN, "camps" },
			{ Language.ES, "campos" }
		}},
		{ "ranking_regions_abbr", new Dictionary<Language, string> {
			{ Language.FR, "rég." },
			{ Language.EN, "reg." },
			{ Language.ES, "reg." }
		}},
		{ "ranking_gold_abbr", new Dictionary<Language, string> {
			{ Language.FR, "or" },
			{ Language.EN, "gold" },
			{ Language.ES, "oro" }
		}},
		{ "ranking_no_data", new Dictionary<Language, string> {
			{ Language.FR, "Aucune donnée" },
			{ Language.EN, "No data" },
			{ Language.ES, "Sin datos" }
		}},
		{ "ranking_player", new Dictionary<Language, string> {
			{ Language.FR, "Joueur" },
			{ Language.EN, "Player" },
			{ Language.ES, "Jugador" }
		}},
		{ "ranking_ai", new Dictionary<Language, string> {
			{ Language.FR, "IA" },
			{ Language.EN, "AI" },
			{ Language.ES, "IA" }
		}},
		{ "ranking_ai_boss", new Dictionary<Language, string> {
			{ Language.FR, "IA Boss" },
			{ Language.EN, "AI Boss" },
			{ Language.ES, "IA Jefe" }
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
