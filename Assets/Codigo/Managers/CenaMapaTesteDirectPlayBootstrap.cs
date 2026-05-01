using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ExoBeasts.Multiplayer.Core;

namespace ExoBeasts.Managers
{
    /// <summary>
    /// Suporte oficial para apertar Play direto em CenaMapaTeste no Editor.
    /// Garante singletons minimos, restaura a equipe do save e sobe um host local limpo.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class CenaMapaTesteDirectPlayBootstrap : MonoBehaviour
    {
        [Header("GameData Bootstrap")]
        [SerializeField] private List<CharacterBase> bibliotecaOriginalPersonagens = new List<CharacterBase>();
        [SerializeField] private CharacterBase[] fallbackTeamSelection = new CharacterBase[8];

        [Header("Direct Play")]
        [SerializeField] private bool autoBootstrapWhenNoNetworkSession = true;
        [SerializeField] private bool allowOutsideEditor = false;
        [SerializeField] private float networkResetTimeoutSeconds = 3f;

        private bool _shouldBootstrap;
        private bool _bootstrapping;

        private void Awake()
        {
            if (!autoBootstrapWhenNoNetworkSession || !CanBootstrapInCurrentBuild())
                return;

            _shouldBootstrap = !GameModeManager.IsNetworkSession;
            if (!_shouldBootstrap)
                return;

            GameModeManager.EnsureInstance();
            GameModeManager.ReturnToSingleplayer();
            PrepareGameData();
        }

        private void Start()
        {
            if (_shouldBootstrap && !_bootstrapping)
                StartCoroutine(BootstrapDirectPlayRoutine());
        }

        private IEnumerator BootstrapDirectPlayRoutine()
        {
            _bootstrapping = true;

            yield return MultiplayerRuntimeReset.ResetToOfflineLocal(networkResetTimeoutSeconds);

            GameModeManager.ReturnToSingleplayer();

            GameDataManager dataManager = PrepareGameData();
            if (dataManager == null)
            {
                _bootstrapping = false;
                yield break;
            }

            PrimeHostCharacterChoice(dataManager);
            yield return StartLocalHostIfNeeded();

            _bootstrapping = false;
        }

        private GameDataManager PrepareGameData()
        {
            GameDataManager dataManager = GameDataManager.EnsureInstance(bibliotecaOriginalPersonagens);
            if (dataManager == null)
            {
                Debug.LogError("[CenaMapaTesteDirectPlayBootstrap] Nao foi possivel inicializar o GameDataManager.");
                return null;
            }

            dataManager.RestaurarSelecao();

            if (!HasPlayableTeam(dataManager))
                ApplyFallbackSelection(dataManager);

            if (!HasPlayableTeam(dataManager))
                Debug.LogError("[CenaMapaTesteDirectPlayBootstrap] Nenhuma equipe valida foi encontrada no save ou no fallback de debug.");

            return dataManager;
        }

        private bool HasPlayableTeam(GameDataManager dataManager)
        {
            if (dataManager == null || dataManager.equipeSelecionada == null || dataManager.equipeSelecionada.Length < 2)
                return false;

            return IsCharacterResolvable(dataManager, dataManager.equipeSelecionada[0]) &&
                   IsCharacterResolvable(dataManager, dataManager.equipeSelecionada[1]);
        }

        private void ApplyFallbackSelection(GameDataManager dataManager)
        {
            if (dataManager == null)
                return;

            dataManager.LimparSelecao();

            int slots = Mathf.Min(dataManager.equipeSelecionada.Length, fallbackTeamSelection.Length);
            for (int i = 0; i < slots; i++)
            {
                CharacterBase fallbackCharacter = fallbackTeamSelection[i];
                if (fallbackCharacter == null)
                    continue;

                CharacterBase runtimeCharacter = FindRuntimeCharacter(dataManager, fallbackCharacter.name);
                if (runtimeCharacter != null)
                    dataManager.equipeSelecionada[i] = runtimeCharacter;
            }

            Debug.LogWarning("[CenaMapaTesteDirectPlayBootstrap] Save sem equipe jogavel. Aplicando fallback de debug para o play direto.");
        }

        private void PrimeHostCharacterChoice(GameDataManager dataManager)
        {
            if (dataManager == null || dataManager.equipeSelecionada == null || dataManager.equipeSelecionada.Length == 0)
                return;

            CharacterBase commander = dataManager.equipeSelecionada[0];
            if (!TryResolveLibraryIndex(dataManager, commander, out int characterIndex))
            {
                Debug.LogError("[CenaMapaTesteDirectPlayBootstrap] Nao foi possivel resolver o comandante local na bibliotecaOriginalPersonagens.");
                return;
            }

            CharacterChoiceCache.SetHostCharacterIndex(characterIndex, "CenaMapaTesteDirectPlayBootstrap");
        }

        private IEnumerator StartLocalHostIfNeeded()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Debug.LogError("[CenaMapaTesteDirectPlayBootstrap] NetworkManager.Singleton nao encontrado em CenaMapaTeste.");
                yield break;
            }

            if (networkManager.IsListening || networkManager.IsHost || networkManager.IsServer || networkManager.IsClient)
                yield break;

            bool started = networkManager.StartHost();
            if (!started)
            {
                Debug.LogError("[CenaMapaTesteDirectPlayBootstrap] StartHost() falhou durante o play direto.");
                yield break;
            }

            Debug.Log("[CenaMapaTesteDirectPlayBootstrap] Host local iniciado para play direto em CenaMapaTeste.");
        }

        private bool IsCharacterResolvable(GameDataManager dataManager, CharacterBase character)
        {
            return TryResolveLibraryIndex(dataManager, character, out _);
        }

        private bool TryResolveLibraryIndex(GameDataManager dataManager, CharacterBase character, out int characterIndex)
        {
            characterIndex = -1;
            if (dataManager == null || character == null || dataManager.bibliotecaOriginalPersonagens == null)
                return false;

            string cleanName = character.name.Replace("(Clone)", "");
            characterIndex = dataManager.bibliotecaOriginalPersonagens.FindIndex(
                candidate => candidate != null && candidate.name == cleanName);

            return characterIndex >= 0;
        }

        private CharacterBase FindRuntimeCharacter(GameDataManager dataManager, string characterName)
        {
            if (dataManager == null || string.IsNullOrEmpty(characterName))
                return null;

            string cleanName = characterName.Replace("(Clone)", "");
            return dataManager.personagensDoJogador.Find(
                candidate => candidate != null && candidate.name.Replace("(Clone)", "") == cleanName);
        }

        private bool CanBootstrapInCurrentBuild()
        {
#if UNITY_EDITOR
            return true;
#else
            return allowOutsideEditor;
#endif
        }
    }
}
