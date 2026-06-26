using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ExoBeasts.Multiplayer.Core
{
    /// <summary>
    /// Workaround para bug do Unity 6 MPPM (confirmado empiricamente em 2026-05-22):
    /// em clones MPPM, <c>SceneManager.sceneCountInBuildSettings</c> (API nativa) retorna 0
    /// mesmo quando <c>EditorBuildSettings.scenes</c> (API managed) tem todas as scenes corretas.
    /// Causa: a camada nativa do Unity não está sincronizada com o EditorBuildSettings em
    /// processos com flag <c>-readonly</c> + <c>-DisableDirectoryMonitor</c>.
    ///
    /// Consequência: NGO 1.12 (<c>NetworkSceneManager.GenerateScenesInBuild()</c>) itera essa
    /// API nativa para construir <c>HashToBuildIndex</c>. Com 0 scenes, a tabela fica vazia.
    /// Cliente lança <c>"Scene Hash X does not exist in HashToBuildIndex table"</c> ao receber
    /// SceneEventType.Synchronize do servidor.
    ///
    /// Fix em camadas (tenta cada uma se anterior falhar):
    /// 1. <see cref="EnsureHashTablePopulated"/>: chama <c>GenerateScenesInBuild()</c> via reflection.
    ///    Funciona se o problema era timing do construtor.
    /// 2. <see cref="PopulateFromEditorBuildSettings"/>: popula HashToBuildIndex/BuildIndexToHash
    ///    diretamente via reflection, usando <c>EditorBuildSettings.scenes</c> + XXHash32
    ///    portado do próprio NGO. Funciona se a API managed estiver OK mas a nativa não.
    /// </summary>
    public static class NetworkSceneTableFixer
    {
        public static void LogPreStart(string context)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[NetworkSceneTableFixer/{context}] PRE-START diagnostic:");
            sb.AppendLine($"  Runtime SceneManager.sceneCountInBuildSettings={SceneManager.sceneCountInBuildSettings}");
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                sb.AppendLine($"    [{i}] {SceneUtility.GetScenePathByBuildIndex(i)}");
            }
#if UNITY_EDITOR
            var editorScenes = UnityEditor.EditorBuildSettings.scenes;
            sb.AppendLine($"  Editor EditorBuildSettings.scenes.Length={editorScenes.Length}");
            for (int i = 0; i < editorScenes.Length; i++)
            {
                var s = editorScenes[i];
                sb.AppendLine($"    [{i}] enabled={s.enabled} {s.path}");
            }

            // Unity 6: globalScenes é a "shared scene list" usada por SceneManager.LoadSceneAsync
            // quando o Active Build Profile não tem a scene. Em clones MPPM esta lista pode
            // estar vazia mesmo com 'scenes' (legado) populado — explica "couldn't be loaded".
            try
            {
                var globalScenes = UnityEditor.EditorBuildSettings.globalScenes;
                sb.AppendLine($"  Editor EditorBuildSettings.globalScenes.Length={globalScenes?.Length ?? -1}");
                if (globalScenes != null)
                {
                    for (int i = 0; i < globalScenes.Length; i++)
                    {
                        var s = globalScenes[i];
                        sb.AppendLine($"    [{i}] enabled={s.enabled} {s.path}");
                    }
                }
            }
            catch (System.Exception ex) { sb.AppendLine($"  globalScenes não acessível: {ex.Message}"); }

            // Active Build Profile (Unity 6): API vive em UnityEditor.Build.Profile.BuildProfile.
            // Usamos reflection para tolerar diferenças entre versões do Unity.
            try
            {
                var (prof, scenes, overrideGlobal) = GetActiveBuildProfileInfo();
                if (prof == null)
                {
                    sb.AppendLine("  activeBuildProfile=NULL (Unity usa shared scene list)");
                }
                else
                {
                    sb.AppendLine($"  activeBuildProfile={prof.GetType().Name} scenes.Length={scenes?.Length ?? -1} overrideGlobal={overrideGlobal}");
                    if (scenes != null)
                    {
                        for (int i = 0; i < scenes.Length; i++)
                        {
                            var s = scenes[i];
                            sb.AppendLine($"    [{i}] enabled={s.enabled} {s.path}");
                        }
                    }
                }
            }
            catch (System.Exception ex) { sb.AppendLine($"  activeBuildProfile não acessível: {ex.GetType().Name}: {ex.Message}"); }
#endif
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Tenta forçar a camada NATIVA do Unity (SceneManager.sceneCountInBuildSettings,
        /// SceneUtility.GetScenePathByBuildIndex) a ressincronizar com EditorBuildSettings.scenes.
        /// Necessário em clones MPPM onde a nativa fica em 0 mesmo com managed correto.
        /// O "touch write" reatribuindo a mesma lista é o gatilho documentado da Unity para
        /// disparar a sincronização interna.
        /// </summary>
        public static void ForceNativeSync(string context)
        {
#if UNITY_EDITOR
            int beforeCount = SceneManager.sceneCountInBuildSettings;
            var canonicalScenes = UnityEditor.EditorBuildSettings.scenes;

            // Touch 1: EditorBuildSettings.scenes (legado)
            try { UnityEditor.EditorBuildSettings.scenes = canonicalScenes; }
            catch (System.Exception ex) { Debug.LogWarning($"[NetworkSceneTableFixer/{context}] touch scenes: {ex.Message}"); }

            // Touch 2: EditorBuildSettings.globalScenes (Unity 6 — "shared scene list")
            // Se globalScenes estiver vazio ou diferente, propaga as scenes do legado.
            try
            {
                var globalScenes = UnityEditor.EditorBuildSettings.globalScenes;
                if (globalScenes == null || globalScenes.Length != canonicalScenes.Length)
                {
                    UnityEditor.EditorBuildSettings.globalScenes = canonicalScenes;
                    Debug.Log($"[NetworkSceneTableFixer/{context}] globalScenes propagado de scenes (Length={canonicalScenes.Length}).");
                }
                else
                {
                    UnityEditor.EditorBuildSettings.globalScenes = globalScenes; // touch
                }
            }
            catch (System.Exception ex) { Debug.LogWarning($"[NetworkSceneTableFixer/{context}] touch globalScenes: {ex.Message}"); }

            // Touch 3: Active Build Profile (Unity 6) — via reflection (API muda entre versões).
            // Se ativo mas com scenes vazias, popula. SceneManager.LoadSceneAsync rejeita scenes
            // que não estão no profile ativo.
            try
            {
                TrySetActiveBuildProfileScenes(canonicalScenes, context);
            }
            catch (System.Exception ex) { Debug.LogWarning($"[NetworkSceneTableFixer/{context}] touch activeBuildProfile: {ex.GetType().Name}: {ex.Message}"); }

            int afterCount = SceneManager.sceneCountInBuildSettings;
            Debug.Log($"[NetworkSceneTableFixer/{context}] ForceNativeSync: sceneCountInBuildSettings antes={beforeCount} depois={afterCount}.");
#endif
        }

        /// <summary>
        /// Chamar IMEDIATAMENTE após StartHost/StartClient.
        /// Estratégia em camadas: tenta GenerateScenesInBuild() primeiro; se ficar vazio,
        /// cai para PopulateFromEditorBuildSettings (fallback Editor-only).
        /// </summary>
        public static void EnsureHashTablePopulated(NetworkManager nm, string context)
        {
            if (nm == null || nm.SceneManager == null)
            {
                Debug.LogWarning($"[NetworkSceneTableFixer/{context}] NetworkManager ou SceneManager null — abortando.");
                return;
            }

            var sceneManagerType = nm.SceneManager.GetType();
            var hashToBuildField = sceneManagerType.GetField("HashToBuildIndex",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            var buildToHashField = sceneManagerType.GetField("BuildIndexToHash",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

            // CAMADA 1: pedir ao NGO regenerar via sua própria API interna.
            var generateMethod = sceneManagerType.GetMethod("GenerateScenesInBuild",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (generateMethod != null)
            {
                try { generateMethod.Invoke(nm.SceneManager, null); }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[NetworkSceneTableFixer/{context}] GenerateScenesInBuild falhou: {ex.Message}");
                }
            }

            int countAfterLayer1 = GetTableCount(hashToBuildField, nm.SceneManager);
            Debug.Log($"[NetworkSceneTableFixer/{context}] Após camada 1 (NGO GenerateScenesInBuild): count={countAfterLayer1}");

            if (countAfterLayer1 > 0)
            {
                LogTable(hashToBuildField, nm.SceneManager, context, "camada 1");
                return;
            }

            // CAMADA 2: fallback Editor-only. Popula manualmente via EditorBuildSettings.scenes.
#if UNITY_EDITOR
            int populated = PopulateFromEditorBuildSettings(nm.SceneManager, hashToBuildField, buildToHashField);
            Debug.Log($"[NetworkSceneTableFixer/{context}] Após camada 2 (fallback manual via EditorBuildSettings): count={populated}");

            if (populated > 0)
            {
                LogTable(hashToBuildField, nm.SceneManager, context, "camada 2");
                return;
            }
#endif

            Debug.LogError($"[NetworkSceneTableFixer/{context}] FALHA TOTAL: HashToBuildIndex segue vazia após todas as camadas. " +
                           $"sceneCountInBuildSettings={SceneManager.sceneCountInBuildSettings}. " +
                           "Cliente vai falhar ao receber SceneEvent do host. " +
                           "Próximo passo: investigar por que a camada nativa do Unity está dessincronizada.");
        }

        private static int GetTableCount(FieldInfo field, object instance)
        {
            if (field == null) return -1;
            return (field.GetValue(instance) as IDictionary)?.Count ?? -1;
        }

        private static void LogTable(FieldInfo field, object instance, string context, string source)
        {
            if (field == null) return;
            if (field.GetValue(instance) is IDictionary dict)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[NetworkSceneTableFixer/{context}] Tabela final ({source}): count={dict.Count}");
                foreach (DictionaryEntry kv in dict)
                {
                    sb.AppendLine($"  hash={kv.Key} buildIndex={kv.Value}");
                }
                Debug.Log(sb.ToString());
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Acessa o Active Build Profile do Unity 6 via reflection (API vive em
        /// UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile()). Retorna o profile,
        /// suas scenes e o flag overrideGlobalSceneList. Tolera ausência da API.
        /// </summary>
        private static (object profile, UnityEditor.EditorBuildSettingsScene[] scenes, bool overrideGlobal) GetActiveBuildProfileInfo()
        {
            // Tipo: UnityEditor.Build.Profile.BuildProfile (assembly UnityEditor.CoreModule)
            var asm = typeof(UnityEditor.EditorBuildSettings).Assembly;
            var profileType = asm.GetType("UnityEditor.Build.Profile.BuildProfile");
            if (profileType == null) return (null, null, false);

            var getActive = profileType.GetMethod("GetActiveBuildProfile",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            if (getActive == null) return (null, null, false);

            var profile = getActive.Invoke(null, null);
            if (profile == null) return (null, null, false);

            // Em Unity 6.0.x o profile expõe 'scenes' (property) e 'overrideGlobalSceneList' (field/prop)
            var scenesMember = (System.Reflection.MemberInfo)profileType.GetProperty("scenes")
                              ?? profileType.GetField("scenes", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                              ?? (System.Reflection.MemberInfo)profileType.GetField("m_Scenes", BindingFlags.NonPublic | BindingFlags.Instance);
            UnityEditor.EditorBuildSettingsScene[] scenes = null;
            if (scenesMember is PropertyInfo pi) scenes = pi.GetValue(profile) as UnityEditor.EditorBuildSettingsScene[];
            else if (scenesMember is FieldInfo fi) scenes = fi.GetValue(profile) as UnityEditor.EditorBuildSettingsScene[];

            var overrideMember = (System.Reflection.MemberInfo)profileType.GetProperty("overrideGlobalSceneList")
                                ?? profileType.GetField("overrideGlobalSceneList", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                ?? (System.Reflection.MemberInfo)profileType.GetField("m_OverrideGlobalSceneList", BindingFlags.NonPublic | BindingFlags.Instance);
            bool overrideGlobal = false;
            if (overrideMember is PropertyInfo pio) overrideGlobal = (bool)(pio.GetValue(profile) ?? false);
            else if (overrideMember is FieldInfo fio) overrideGlobal = (bool)(fio.GetValue(profile) ?? false);

            return (profile, scenes, overrideGlobal);
        }

        /// <summary>
        /// Tenta popular o profile ativo com a lista canônica via reflection.
        /// No-op se nenhum profile está ativo, se já tem scenes, ou se a API não existe.
        /// </summary>
        private static void TrySetActiveBuildProfileScenes(UnityEditor.EditorBuildSettingsScene[] canonicalScenes, string context)
        {
            var asm = typeof(UnityEditor.EditorBuildSettings).Assembly;
            var profileType = asm.GetType("UnityEditor.Build.Profile.BuildProfile");
            if (profileType == null)
            {
                Debug.Log($"[NetworkSceneTableFixer/{context}] BuildProfile API não disponível nesta versão do Unity — pulando touch profile.");
                return;
            }
            var getActive = profileType.GetMethod("GetActiveBuildProfile",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            var profile = getActive?.Invoke(null, null);
            if (profile == null)
            {
                Debug.Log($"[NetworkSceneTableFixer/{context}] activeBuildProfile=null — sem touch necessário no profile.");
                return;
            }

            var scenesProp = profileType.GetProperty("scenes");
            var scenesField = profileType.GetField("scenes", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                              ?? profileType.GetField("m_Scenes", BindingFlags.NonPublic | BindingFlags.Instance);

            UnityEditor.EditorBuildSettingsScene[] current = null;
            if (scenesProp != null) current = scenesProp.GetValue(profile) as UnityEditor.EditorBuildSettingsScene[];
            else if (scenesField != null) current = scenesField.GetValue(profile) as UnityEditor.EditorBuildSettingsScene[];

            // Sempre setar (touch ou popular). Tenta property primeiro, fallback field.
            try
            {
                if (scenesProp != null && scenesProp.CanWrite)
                {
                    scenesProp.SetValue(profile, canonicalScenes);
                    Debug.Log($"[NetworkSceneTableFixer/{context}] activeBuildProfile.scenes setado via property (Length={canonicalScenes.Length}, anterior={current?.Length ?? -1}).");
                }
                else if (scenesField != null)
                {
                    scenesField.SetValue(profile, canonicalScenes);
                    Debug.Log($"[NetworkSceneTableFixer/{context}] activeBuildProfile.scenes setado via field (Length={canonicalScenes.Length}, anterior={current?.Length ?? -1}).");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[NetworkSceneTableFixer/{context}] Falha ao setar profile.scenes: {ex.GetType().Name}: {ex.Message}");
            }
        }
#endif

#if UNITY_EDITOR
        /// <summary>
        /// Popula HashToBuildIndex/BuildIndexToHash diretamente usando EditorBuildSettings.scenes
        /// (a API managed que sabemos estar correta no clone) e XXHash32 portado do NGO.
        /// Retorna o número de scenes populadas.
        /// </summary>
        private static int PopulateFromEditorBuildSettings(object sceneManagerInstance,
                                                            FieldInfo hashToBuildField,
                                                            FieldInfo buildToHashField)
        {
            if (hashToBuildField == null || buildToHashField == null)
            {
                Debug.LogError("[NetworkSceneTableFixer] HashToBuildIndex/BuildIndexToHash não encontrados via reflection. NGO API mudou.");
                return 0;
            }

            var hashToBuild = hashToBuildField.GetValue(sceneManagerInstance) as IDictionary;
            var buildToHash = buildToHashField.GetValue(sceneManagerInstance) as IDictionary;
            if (hashToBuild == null || buildToHash == null)
            {
                Debug.LogError("[NetworkSceneTableFixer] Tabelas internas não são IDictionary.");
                return 0;
            }

            hashToBuild.Clear();
            buildToHash.Clear();

            // Popular também o fallback hash->path do nosso patch em NGO.
            // Esse fallback é consultado por ScenePathFromHash quando SceneUtility.GetScenePathByBuildIndex
            // retorna vazio (caso típico de clone MPPM com camada nativa quebrada).
            var fallback = new Dictionary<uint, string>();

            var editorScenes = UnityEditor.EditorBuildSettings.scenes;
            int buildIndex = 0;
            int populated = 0;
            foreach (var s in editorScenes)
            {
                if (!s.enabled) { buildIndex++; continue; }
                uint hash = Hash32(s.path);
                if (!hashToBuild.Contains(hash))
                {
                    hashToBuild.Add(hash, buildIndex);
                    buildToHash.Add(buildIndex, hash);
                    fallback[hash] = s.path;
                    populated++;
                }
                buildIndex++;
            }

            // Substituir o fallback global (estático). Última StartClient ganha — em MPPM com
            // múltiplos clones isso ainda é correto porque todos os clones leem o mesmo
            // EditorBuildSettings via junction.
            TrySetNetcodeFallbackHashToPath(fallback);

            return populated;
        }
#endif

        public static bool TryGetFallbackScenePath(string sceneNameOrPath, out string scenePath)
        {
            scenePath = null;

            if (!TryGetNetcodeFallbackHashToPath(out var fallback) || fallback == null)
            {
                return false;
            }

            if (fallback.TryGetValue(Hash32(sceneNameOrPath), out scenePath))
            {
                return true;
            }

            string canonicalPath = ToCanonicalScenePath(sceneNameOrPath);
            return !string.Equals(canonicalPath, sceneNameOrPath, System.StringComparison.Ordinal) &&
                   fallback.TryGetValue(Hash32(canonicalPath), out scenePath);
        }

        private static string ToCanonicalScenePath(string sceneNameOrPath)
        {
            if (string.IsNullOrEmpty(sceneNameOrPath))
            {
                return sceneNameOrPath;
            }

            string normalized = sceneNameOrPath.Replace('\\', '/');
            if (normalized.StartsWith("Assets/", System.StringComparison.Ordinal) &&
                normalized.EndsWith(".unity", System.StringComparison.Ordinal))
            {
                return normalized;
            }

            return "Assets/Cenas/" + normalized + ".unity";
        }

        private static bool TryGetNetcodeFallbackHashToPath(out Dictionary<uint, string> fallback)
        {
            fallback = null;
            var field = typeof(NetworkSceneManager).GetField(
                "MppmFallbackHashToPath",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            if (field == null || !typeof(Dictionary<uint, string>).IsAssignableFrom(field.FieldType))
            {
                return false;
            }

            fallback = field.GetValue(null) as Dictionary<uint, string>;
            return fallback != null;
        }

        private static void TrySetNetcodeFallbackHashToPath(Dictionary<uint, string> fallback)
        {
            var field = typeof(NetworkSceneManager).GetField(
                "MppmFallbackHashToPath",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            if (field == null || !field.FieldType.IsAssignableFrom(typeof(Dictionary<uint, string>)))
            {
                Debug.Log("[NetworkSceneTableFixer] Netcode sem MppmFallbackHashToPath; fallback hash->path indisponivel.");
                return;
            }

            field.SetValue(null, fallback);
            Debug.Log($"[NetworkSceneTableFixer] fallback hash->path do Netcode populado com {fallback.Count} entradas.");
        }

        /// <summary>
        /// XXHash32 portado de <c>Unity.Netcode.XXHash.Hash32(byte*, int, uint)</c> com seed=0.
        /// Necessário porque a classe XXHash do NGO é internal, então não dá pra chamar direto
        /// de outro assembly. Implementação validada contra targets observados nos logs:
        /// "Assets/Cenas/EscolherPersonagem.unity" -> hash observado para o path canonico;
        /// "Assets/Cenas/LobbyScene.unity" -> hash observado para o path canonico.
        /// </summary>
        public static uint Hash32(string text)
        {
            var data = Encoding.UTF8.GetBytes(text);
            int length = data.Length;
            const uint PRIME1 = 2654435761u;
            const uint PRIME2 = 2246822519u;
            const uint PRIME3 = 3266489917u;
            const uint PRIME4 = 668265263u;
            const uint PRIME5 = 374761393u;

            uint hash = PRIME5;
            int idx = 0;

            unchecked
            {
                if (length >= 16)
                {
                    uint v0 = PRIME1 + PRIME2;
                    uint v1 = PRIME2;
                    uint v2 = 0u;
                    uint v3 = 0u - PRIME1;

                    int count = length >> 4;
                    for (int i = 0; i < count; i++)
                    {
                        uint p0 = System.BitConverter.ToUInt32(data, idx + 0);
                        uint p1 = System.BitConverter.ToUInt32(data, idx + 4);
                        uint p2 = System.BitConverter.ToUInt32(data, idx + 8);
                        uint p3 = System.BitConverter.ToUInt32(data, idx + 12);

                        v0 += p0 * PRIME2; v0 = (v0 << 13) | (v0 >> 19); v0 *= PRIME1;
                        v1 += p1 * PRIME2; v1 = (v1 << 13) | (v1 >> 19); v1 *= PRIME1;
                        v2 += p2 * PRIME2; v2 = (v2 << 13) | (v2 >> 19); v2 *= PRIME1;
                        v3 += p3 * PRIME2; v3 = (v3 << 13) | (v3 >> 19); v3 *= PRIME1;

                        idx += 16;
                    }

                    hash = ((v0 << 1) | (v0 >> 31))
                         + ((v1 << 7) | (v1 >> 25))
                         + ((v2 << 12) | (v2 >> 20))
                         + ((v3 << 18) | (v3 >> 14));
                }

                hash += (uint)length;

                int remaining = length & 15;
                while (remaining >= 4)
                {
                    uint p = System.BitConverter.ToUInt32(data, idx);
                    hash += p * PRIME3;
                    hash = ((hash << 17) | (hash >> 15)) * PRIME4;
                    idx += 4;
                    remaining -= 4;
                }
                while (remaining > 0)
                {
                    hash += data[idx] * PRIME5;
                    hash = ((hash << 11) | (hash >> 21)) * PRIME1;
                    idx++;
                    remaining--;
                }

                hash ^= hash >> 15;
                hash *= PRIME2;
                hash ^= hash >> 13;
                hash *= PRIME3;
                hash ^= hash >> 16;
            }

            return hash;
        }
    }
}
