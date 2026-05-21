using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System;
using System.IO;
using ExoBeasts.Multiplayer.Core;

namespace ExoBeasts.Multiplayer.Editor
{
    public class EOSConfigGenerator : IPreprocessBuildWithReport
    {
        private const string EOS_DIR = "Assets/StreamingAssets/EOS";
        private const string CREDENTIALS_FILE = "EOSCredentials.json";

        public int callbackOrder => -100;

        [InitializeOnLoadMethod]
        private static void RegisterPlayModeHook()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;

            if (!TryGenerate(out string error))
            {
                Debug.LogError($"[EOSConfigGenerator] {error}");
                Debug.LogError("[EOSConfigGenerator] Play Mode bloqueado — corrija as credenciais EOS.");
                EditorApplication.isPlaying = false;
            }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!TryGenerate(out string error))
            {
                throw new BuildFailedException(
                    $"[EOSConfigGenerator] Build cancelada: {error}\n\n" +
                    "Opcoes:\n" +
                    "  1. Defina variaveis de ambiente (EOS_PRODUCT_ID, EOS_SANDBOX_ID, EOS_DEPLOYMENT_ID, EOS_CLIENT_ID, EOS_CLIENT_SECRET)\n" +
                    "  2. Crie EOSCredentials.json na raiz do projeto (copie de EOSCredentials.json.template)\n");
            }
        }

        [MenuItem("Tools/ExoBeasts/Generate EOS Config")]
        public static void GenerateFromMenu()
        {
            if (TryGenerate(out string error))
            {
                EditorUtility.DisplayDialog("EOS Config",
                    "Configuracoes EOS geradas com sucesso em:\nAssets/StreamingAssets/EOS/", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("EOS Config — Erro", error, "OK");
            }
        }

        public static bool TryGenerate(out string error)
        {
            EOSCredentialData creds;

            if (TryLoadFromEnvironment(out creds))
            {
                Debug.Log("[EOSConfigGenerator] Credenciais carregadas de variaveis de ambiente.");
            }
            else if (TryLoadFromFile(out creds))
            {
                Debug.Log("[EOSConfigGenerator] Credenciais carregadas de EOSCredentials.json.");
            }
            else
            {
                error =
                    "Nenhuma fonte de credenciais EOS encontrada.\n\n" +
                    "Opcao A — Variaveis de ambiente:\n" +
                    "  EOS_PRODUCT_ID, EOS_SANDBOX_ID, EOS_DEPLOYMENT_ID,\n" +
                    "  EOS_CLIENT_ID, EOS_CLIENT_SECRET, EOS_ENCRYPTION_KEY (opcional)\n\n" +
                    "Opcao B — Arquivo local:\n" +
                    "  Crie EOSCredentials.json na raiz do projeto.\n" +
                    "  Use EOSCredentials.json.template como modelo.";
                return false;
            }

            string validation = ValidateCredentials(creds);
            if (validation != null)
            {
                error = $"Credenciais EOS invalidas: {validation}";
                return false;
            }

            WriteConfigFiles(creds);
            LogSafeCredentialInfo(creds);

            error = null;
            return true;
        }

        private static bool TryLoadFromEnvironment(out EOSCredentialData creds)
        {
            creds = new EOSCredentialData();

            string productId = Environment.GetEnvironmentVariable("EOS_PRODUCT_ID");
            string clientId = Environment.GetEnvironmentVariable("EOS_CLIENT_ID");

            if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(clientId))
            {
                return false;
            }

            creds.ProductId = productId;
            creds.SandboxId = Environment.GetEnvironmentVariable("EOS_SANDBOX_ID") ?? "";
            creds.DeploymentId = Environment.GetEnvironmentVariable("EOS_DEPLOYMENT_ID") ?? "";
            creds.ClientId = clientId;
            creds.ClientSecret = Environment.GetEnvironmentVariable("EOS_CLIENT_SECRET") ?? "";
            creds.EncryptionKey = Environment.GetEnvironmentVariable("EOS_ENCRYPTION_KEY") ?? "";
            creds.Environment = Environment.GetEnvironmentVariable("EOS_ENVIRONMENT") ?? "Development";
            creds.Source = "environment";
            return true;
        }

        private static bool TryLoadFromFile(out EOSCredentialData creds)
        {
            creds = new EOSCredentialData();

            // BUG FIX (2026-05-21): em clones MPPM, Application.dataPath aponta para a copia
            // virtual em %LocalAppData%\Unity\Editor\MultiplayerPlayMode\... — nao para a raiz
            // do projeto original onde EOSCredentials.json reside. Replicamos o pattern
            // ja validado em Assets/Codigo/Multiplayer/Core/EOSConfig.cs:72-76 (TryLoadFromFile
            // do runtime). Sem isso, Play Mode no Player 2 do MPPM bloqueia com
            // "Nenhuma fonte de credenciais EOS encontrada".
            string projectRoot = MppmHelper.IsClone
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", ".."))
                : Path.GetDirectoryName(Application.dataPath);
            string filePath = Path.Combine(projectRoot, CREDENTIALS_FILE);

            if (!File.Exists(filePath))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                var parsed = JsonUtility.FromJson<EOSCredentialFileFormat>(json);

                creds.ProductId = parsed.ProductId ?? "";
                creds.SandboxId = parsed.SandboxId ?? "";
                creds.DeploymentId = parsed.DeploymentId ?? "";
                creds.ClientId = parsed.ClientId ?? "";
                creds.ClientSecret = parsed.ClientSecret ?? "";
                creds.EncryptionKey = parsed.EncryptionKey ?? "";
                creds.Environment = string.IsNullOrEmpty(parsed.Environment) ? "Development" : parsed.Environment;
                creds.Source = "file";
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EOSConfigGenerator] Erro ao ler {CREDENTIALS_FILE}: {e.Message}");
                return false;
            }
        }

        private static string ValidateCredentials(EOSCredentialData creds)
        {
            if (string.IsNullOrEmpty(creds.ProductId))
                return "ProductId ausente" + SourceHint(creds);
            if (string.IsNullOrEmpty(creds.SandboxId))
                return "SandboxId ausente" + SourceHint(creds);
            if (string.IsNullOrEmpty(creds.DeploymentId))
                return "DeploymentId ausente" + SourceHint(creds);
            if (string.IsNullOrEmpty(creds.ClientId))
                return "ClientId ausente" + SourceHint(creds);
            if (string.IsNullOrEmpty(creds.ClientSecret))
                return "ClientSecret ausente" + SourceHint(creds);

            if (!IsValidEncryptionKey(creds.EncryptionKey))
                return $"EncryptionKey deve ter 64 caracteres hex (encontrado: {creds.EncryptionKey?.Length ?? 0})";

            return null;
        }

        private static string SourceHint(EOSCredentialData creds)
        {
            return creds.Source == "environment"
                ? " (verifique a variavel de ambiente correspondente)"
                : " (verifique EOSCredentials.json)";
        }

        private static void WriteConfigFiles(EOSCredentialData creds)
        {
            if (!Directory.Exists(EOS_DIR))
            {
                Directory.CreateDirectory(EOS_DIR);
            }

            WriteProductConfig(creds);
            WriteWindowsConfig(creds);
            WriteLegacyConfig(creds);

            AssetDatabase.Refresh();
        }

        private static void WriteProductConfig(EOSCredentialData creds)
        {
            string envName = creds.Environment;
            string json = $@"{{
    ""ProductName"": ""ExoBeasts"",
    ""ProductId"": ""{Escape(creds.ProductId)}"",
    ""ProductVersion"": ""1.0.0"",
    ""imported"": true,
    ""Clients"": [
        {{
            ""Name"": ""DefaultClient"",
            ""Value"": {{
                ""ClientId"": ""{Escape(creds.ClientId)}"",
                ""ClientSecret"": ""{Escape(creds.ClientSecret)}"",
                ""encryptionKey"": ""{Escape(creds.EncryptionKey)}""
            }}
        }}
    ],
    ""Environments"": {{
        ""Deployments"": [
            {{
                ""Name"": ""{Escape(envName)}"",
                ""Value"": {{
                    ""SandboxId"": {{
                        ""Value"": ""{Escape(creds.SandboxId)}""
                    }},
                    ""DeploymentId"": ""{Escape(creds.DeploymentId)}""
                }}
            }}
        ],
        ""Sandboxes"": [
            {{
                ""Name"": ""{Escape(envName)}"",
                ""Value"": {{
                    ""Value"": ""{Escape(creds.SandboxId)}""
                }}
            }}
        ]
    }},
    ""schemaVersion"": ""1.0""
}}";
            File.WriteAllText(Path.Combine(EOS_DIR, "eos_product_config.json"), json);
        }

        private static void WriteWindowsConfig(EOSCredentialData creds)
        {
            string json = $@"{{
    ""deployment"": {{
        ""SandboxId"": {{
            ""Value"": ""{Escape(creds.SandboxId)}""
        }},
        ""DeploymentId"": ""{Escape(creds.DeploymentId)}""
    }},
    ""clientCredentials"": {{
        ""ClientId"": ""{Escape(creds.ClientId)}"",
        ""ClientSecret"": ""{Escape(creds.ClientSecret)}"",
        ""encryptionKey"": ""{Escape(creds.EncryptionKey)}""
    }},
    ""isServer"": false,
    ""platformOptionsFlags"": ""None"",
    ""authScopeOptionsFlags"": ""BasicProfile, FriendsList, Presence"",
    ""integratedPlatformManagementFlags"": ""Disabled"",
    ""tickBudgetInMilliseconds"": 0,
    ""taskNetworkTimeoutSeconds"": 0.0,
    ""alwaysSendInputToOverlay"": false,
    ""initialButtonDelayForOverlay"": 0.0,
    ""repeatButtonDelayForOverlay"": 0.0,
    ""toggleFriendsButtonCombination"": ""SpecialLeft"",
    ""schemaVersion"": ""1.0""
}}";
            File.WriteAllText(Path.Combine(EOS_DIR, "eos_windows_config.json"), json);
        }

        private static void WriteLegacyConfig(EOSCredentialData creds)
        {
            string json = $@"{{
    ""deploymentID"": ""{Escape(creds.DeploymentId)}"",
    ""clientID"": ""{Escape(creds.ClientId)}"",
    ""clientSecret"": ""{Escape(creds.ClientSecret)}"",
    ""encryptionKey"": ""{Escape(creds.EncryptionKey)}"",
    ""tickBudgetInMilliseconds"": 0,
    ""taskNetworkTimeoutSeconds"": 0.0,
    ""platformOptionsFlags"": ""None"",
    ""authScopeOptionsFlags"": ""BasicProfile, FriendsList, Presence"",
    ""integratedPlatformManagementFlags"": 0,
    ""alwaysSendInputToOverlay"": false,
    ""schemaVersion"": ""1.0""
}}";
            File.WriteAllText(Path.Combine(EOS_DIR, "EpicOnlineServicesConfig.json"), json);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        private static bool IsValidEncryptionKey(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool isHex = (c >= '0' && c <= '9') ||
                             (c >= 'a' && c <= 'f') ||
                             (c >= 'A' && c <= 'F');
                if (!isHex)
                    return false;
            }

            return true;
        }

        private static void LogSafeCredentialInfo(EOSCredentialData creds)
        {
            string clientIdMasked = creds.ClientId.Length > 8
                ? creds.ClientId.Substring(0, 8) + "..."
                : "***";
            string source = creds.Source == "environment" ? "variaveis de ambiente" : "EOSCredentials.json";

            Debug.Log(
                $"[EOSConfigGenerator] Configs EOS gerados com sucesso.\n" +
                $"  Fonte: {source}\n" +
                $"  Ambiente: {creds.Environment}\n" +
                $"  ProductId: {creds.ProductId}\n" +
                $"  SandboxId: {creds.SandboxId}\n" +
                $"  DeploymentId: {creds.DeploymentId}\n" +
                $"  ClientId: {clientIdMasked}\n" +
                $"  ClientSecret: [REDACTED]\n" +
                $"  EncryptionKey: {(string.IsNullOrEmpty(creds.EncryptionKey) ? "[nao definida]" : "[REDACTED]")}");
        }

        internal class EOSCredentialData
        {
            public string ProductId;
            public string SandboxId;
            public string DeploymentId;
            public string ClientId;
            public string ClientSecret;
            public string EncryptionKey;
            public string Environment;
            public string Source;
        }

        [Serializable]
        private class EOSCredentialFileFormat
        {
            public string ProductId;
            public string SandboxId;
            public string DeploymentId;
            public string ClientId;
            public string ClientSecret;
            public string EncryptionKey;
            public string Environment;
        }
    }
}
