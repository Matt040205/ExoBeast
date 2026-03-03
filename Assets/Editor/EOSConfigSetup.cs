using UnityEngine;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExoBeasts.Multiplayer.Setup
{
    /// <summary>
    /// Utilitario para configurar o plugin EOS usando credenciais externas
    /// Execute via menu: Tools > ExoBeasts > Setup EOS Config
    /// </summary>
    public static class EOSConfigSetup
    {
        private const string CREDENTIALS_FILE = "EOSCredentials.json";
        private const string STREAMING_ASSETS_EOS = "Assets/StreamingAssets/EOS";

        [UnityEditor.MenuItem("Tools/ExoBeasts/Setup EOS Config")]
        public static void SetupEOSConfiguration()
        {
            Debug.Log("[EOSConfigSetup] Iniciando configuracao do EOS Plugin...");

            try
            {
                // 1. Ler credenciais do arquivo externo
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                string credentialsPath = Path.Combine(projectRoot, CREDENTIALS_FILE);

                if (!File.Exists(credentialsPath))
                {
                    Debug.LogError($"[EOSConfigSetup] Arquivo nao encontrado: {credentialsPath}");
                    UnityEditor.EditorUtility.DisplayDialog("Erro",
                        $"Arquivo EOSCredentials.json nao encontrado em:\n{credentialsPath}",
                        "OK");
                    return;
                }

                Debug.Log($"[EOSConfigSetup] Lendo credenciais de: {credentialsPath}");
                string credentialsJson = File.ReadAllText(credentialsPath);
                JObject credentials = JObject.Parse(credentialsJson);

                // 2. Extrair valores
                string productId = credentials["ProductId"]?.ToString();
                string productName = credentials["ProductName"]?.ToString() ?? "ExoBeasts";
                string sandboxId = credentials["SandboxId"]?.ToString();
                string deploymentId = credentials["DeploymentId"]?.ToString();
                string clientId = credentials["ClientId"]?.ToString();
                string clientSecret = credentials["ClientSecret"]?.ToString();
                string encryptionKey = credentials["EncryptionKey"]?.ToString();

                // Validar
                if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(sandboxId) ||
                    string.IsNullOrEmpty(deploymentId) || string.IsNullOrEmpty(clientId) ||
                    string.IsNullOrEmpty(clientSecret))
                {
                    Debug.LogError("[EOSConfigSetup] Credenciais incompletas!");
                    UnityEditor.EditorUtility.DisplayDialog("Erro",
                        "EOSCredentials.json esta com campos faltando!",
                        "OK");
                    return;
                }

                Debug.Log("[EOSConfigSetup] Credenciais validadas com sucesso!");

                // 3. Criar diretorio StreamingAssets/EOS
                if (!Directory.Exists(STREAMING_ASSETS_EOS))
                {
                    Directory.CreateDirectory(STREAMING_ASSETS_EOS);
                    Debug.Log($"[EOSConfigSetup] Diretorio criado: {STREAMING_ASSETS_EOS}");
                }

                // 4. Criar eos_product_config.json
                var productConfig = new
                {
                    ProductName = productName,
                    ProductId = productId,
                    ProductVersion = "1.0.0",
                    imported = true,
                    Clients = new[]
                    {
                        new
                        {
                            Name = "Default",
                            Value = new
                            {
                                ClientId = clientId,
                                ClientSecret = clientSecret,
                                EncryptionKey = encryptionKey ?? ""
                            }
                        }
                    },
                    Environments = new
                    {
                        Sandboxes = new[]
                        {
                            new
                            {
                                Name = "Default",
                                Value = new
                                {
                                    Value = sandboxId
                                }
                            }
                        },
                        Deployments = new[]
                        {
                            new
                            {
                                Name = "Default",
                                Value = new
                                {
                                    DeploymentId = deploymentId,
                                    SandboxId = new
                                    {
                                        Value = sandboxId
                                    }
                                }
                            }
                        }
                    },
                    schemaVersion = "1.0"
                };

                string productConfigPath = Path.Combine(STREAMING_ASSETS_EOS, "eos_product_config.json");
                string productConfigJson = JsonConvert.SerializeObject(productConfig, Formatting.Indented);
                File.WriteAllText(productConfigPath, productConfigJson);
                Debug.Log($"[EOSConfigSetup] ProductConfig.json criado em: {productConfigPath}");

                // 5. Criar eos_windows_config.json
                var platformConfig = new
                {
                    deployment = new
                    {
                        SandboxId = new
                        {
                            Value = sandboxId
                        },
                        DeploymentId = deploymentId
                    },
                    clientCredentials = new
                    {
                        ClientId = clientId,
                        ClientSecret = clientSecret,
                        EncryptionKey = encryptionKey ?? ""
                    },
                    isServer = false,
                    platformOptionsFlags = "None",
                    authScopeOptionsFlags = "BasicProfile, FriendsList, Presence",
                    integratedPlatformManagementFlags = "Disabled",
                    tickBudgetInMilliseconds = 0,
                    taskNetworkTimeoutSeconds = 0.0,
                    threadAffinity = new
                    {
                        NetworkWork = 0,
                        StorageIo = 0,
                        WebSocketIo = 0,
                        P2PIo = 0,
                        HttpRequestIo = 0,
                        RTCIo = 0
                    },
                    alwaysSendInputToOverlay = false,
                    initialButtonDelayForOverlay = 0.0,
                    repeatButtonDelayForOverlay = 0.0,
                    toggleFriendsButtonCombination = "SpecialLeft",
                    schemaVersion = "1.0"
                };

                string platformConfigPath = Path.Combine(STREAMING_ASSETS_EOS, "eos_windows_config.json");
                string platformConfigJson = JsonConvert.SerializeObject(platformConfig, Formatting.Indented);
                File.WriteAllText(platformConfigPath, platformConfigJson);
                Debug.Log($"[EOSConfigSetup] PlatformConfig.json criado em: {platformConfigPath}");

                // 6. Refresh AssetDatabase
                UnityEditor.AssetDatabase.Refresh();

                Debug.Log("[EOSConfigSetup] ✓ Configuracao do EOS Plugin concluida com sucesso!");
                UnityEditor.EditorUtility.DisplayDialog("Sucesso!",
                    "Configuracao do EOS Plugin concluida!\n\n" +
                    "Arquivos criados em:\n" +
                    "Assets/StreamingAssets/EOS/\n\n" +
                    "O Unity vai recompilar agora.",
                    "OK");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[EOSConfigSetup] Erro: {e.Message}\n{e.StackTrace}");
                UnityEditor.EditorUtility.DisplayDialog("Erro",
                    $"Erro ao configurar EOS:\n{e.Message}",
                    "OK");
            }
        }
    }
}
