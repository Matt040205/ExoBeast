using System;
using System.Collections.Generic;

namespace ExoBeasts.Multiplayer.Lobby
{
    /// <summary>
    /// Estruturas de dados para o sistema de lobby
    /// </summary>

    [Serializable]
    public class LobbyInfo
    {
        public string lobbyId;
        public string lobbyName;
        public string hostDisplayName;
        public int currentPlayers;
        public int maxPlayers = 4;
        public string mapName;
        public bool isPublic;
        public LobbyState state;

        public LobbyInfo()
        {
            lobbyId = "";
            lobbyName = "Nova Sala";
            hostDisplayName = "Host";
            currentPlayers = 0;
            maxPlayers = 4;
            mapName = "CenaMapaTeste";
            isPublic = true;
            state = LobbyState.WaitingForPlayers;
        }
    }

    [Serializable]
    public class LobbyMember
    {
        public string productUserId;
        public string displayName;
        public int selectedCharacterIndex = -1; // -1 = nenhum personagem selecionado
        public bool isReady;
        public bool isHost;

        public LobbyMember()
        {
            productUserId = "";
            displayName = "Player";
            selectedCharacterIndex = -1;
            isReady = false;
            isHost = false;
        }

        public LobbyMember(string userId, string name, bool host = false)
        {
            productUserId = userId;
            displayName = name;
            selectedCharacterIndex = -1;
            isReady = false;
            isHost = host;
        }
    }

    [Serializable]
    public enum LobbyState
    {
        WaitingForPlayers,      // Aguardando jogadores entrarem
        SelectingCharacters,    // Jogadores selecionando personagens
        StartingMatch,          // Iniciando partida (alocando servidor)
        InGame                  // Partida em andamento
    }

    [Serializable]
    public class LobbySearchFilter
    {
        public string lobbyName = "";
        public bool onlyPublic = true;
        public int maxResults = 10;

        public LobbySearchFilter()
        {
            lobbyName = "";
            onlyPublic = true;
            maxResults = 10;
        }
    }

    [Serializable]
    public class LobbySettings
    {
        public string lobbyName;
        public int maxPlayers = 4;
        public bool isPublic = true;
        public string mapName = "CenaMapaTeste";

        public LobbySettings()
        {
            lobbyName = "Sala do Jogador";
            maxPlayers = 4;
            isPublic = true;
            mapName = "CenaMapaTeste";
        }
    }

    /// <summary>
    /// Atributos do lobby que serao armazenados no Epic Lobby Service
    /// </summary>
    public static class LobbyAttributes
    {
        public const string LOBBY_NAME = "LOBBY_NAME";
        public const string MAP_NAME = "MAP_NAME";
        public const string MAX_PLAYERS = "MAX_PLAYERS";
        public const string CURRENT_PLAYERS = "CURRENT_PLAYERS";
        public const string IS_PUBLIC = "IS_PUBLIC";
        public const string LOBBY_STATE = "LOBBY_STATE";
        public const string SERVER_ADDRESS = "SERVER_ADDRESS";
        public const string SERVER_PORT = "SERVER_PORT";
    }

    /// <summary>
    /// Atributos de membro do lobby
    /// </summary>
    public static class MemberAttributes
    {
        public const string DISPLAY_NAME = "DISPLAY_NAME";
        public const string CHARACTER_INDEX = "CHARACTER_INDEX";
        public const string IS_READY = "IS_READY";
        public const string IS_HOST = "IS_HOST";
    }
}
