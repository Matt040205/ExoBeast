using UnityEngine;
using UnityEditor;
using System.IO;

namespace ExoBeasts.Multiplayer.Editor
{
    /// <summary>
    /// Editor script para importar credenciais do EOSCredentials.json para o PlayEveryWare
    /// Execute via menu: Tools > EOS > Import Credentials
    /// </summary>
    public class EOSConfigImporter : EditorWindow
    {
        [MenuItem("Tools/EOS/Import Credentials from EOSCredentials.json")]
        public static void ImportCredentials()
        {
            string credentialsPath = Path.Combine(Application.dataPath, "..", "EOSCredentials.json");

            if (!File.Exists(credentialsPath))
            {
                EditorUtility.DisplayDialog("Erro",
                    "Arquivo EOSCredentials.json nao encontrado na raiz do projeto!\n\n" +
                    "Crie o arquivo com suas credenciais da Epic.", "OK");
                return;
            }

            try
            {
                string json = File.ReadAllText(credentialsPath);
                EOSCredentialsData credentials = JsonUtility.FromJson<EOSCredentialsData>(json);

                if (string.IsNullOrEmpty(credentials.ProductId) ||
                    string.IsNullOrEmpty(credentials.ClientId))
                {
                    EditorUtility.DisplayDialog("Erro",
                        "EOSCredentials.json esta incompleto!\n\n" +
                        "Verifique se ProductId, SandboxId, DeploymentId, ClientId e ClientSecret estao preenchidos.", "OK");
                    return;
                }

                // Criar pasta StreamingAssets/EOS se nao existir
                string eosConfigPath = Path.Combine(Application.streamingAssetsPath, "EOS");
                if (!Directory.Exists(eosConfigPath))
                {
                    Directory.CreateDirectory(eosConfigPath);
                }

                // Criar ProductConfig
                CreateProductConfig(eosConfigPath, credentials);

                // Criar WindowsConfig
                CreateWindowsConfig(eosConfigPath, credentials);

                // Criar EpicOnlineServicesConfig (legado)
                CreateLegacyConfig(eosConfigPath, credentials);

                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("Sucesso",
                    "Credenciais importadas com sucesso!\n\n" +
                    "Arquivos criados em Assets/StreamingAssets/EOS/\n\n" +
                    "Reinicie o Play Mode para testar.", "OK");

                Debug.Log("[EOSConfigImporter] Credenciais importadas com sucesso!");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Erro",
                    $"Erro ao importar credenciais:\n{e.Message}", "OK");
                Debug.LogError($"[EOSConfigImporter] Erro: {e}");
            }
        }

        private static void CreateProductConfig(string path, EOSCredentialsData creds)
        {
            var config = new ProductConfigData
            {
                ProductName = "ExoBeasts",
                ProductId = creds.ProductId,
                ProductVersion = "1.0.0",
                imported = true,
                Clients = new ClientData[]
                {
                    new ClientData
                    {
                        Name = "DefaultClient",
                        Value = new ClientValueData
                        {
                            ClientId = creds.ClientId,
                            ClientSecret = creds.ClientSecret
                        }
                    }
                },
                Environments = new EnvironmentsData
                {
                    Deployments = new DeploymentData[]
                    {
                        new DeploymentData
                        {
                            Name = "Development",
                            Value = new DeploymentValueData
                            {
                                SandboxId = new SandboxIdData { Value = creds.SandboxId },
                                DeploymentId = creds.DeploymentId
                            }
                        }
                    },
                    Sandboxes = new SandboxData[]
                    {
                        new SandboxData
                        {
                            Name = "Development",
                            Value = creds.SandboxId
                        }
                    }
                },
                schemaVersion = "1.0"
            };

            string json = JsonUtility.ToJson(config, true);
            File.WriteAllText(Path.Combine(path, "eos_product_config.json"), json);
            Debug.Log("[EOSConfigImporter] ProductConfig criado");
        }

        private static void CreateWindowsConfig(string path, EOSCredentialsData creds)
        {
            var config = new WindowsConfigData
            {
                deployment = new DeploymentValueData
                {
                    SandboxId = new SandboxIdData { Value = creds.SandboxId },
                    DeploymentId = creds.DeploymentId
                },
                clientCredentials = new ClientValueData
                {
                    ClientId = creds.ClientId,
                    ClientSecret = creds.ClientSecret
                },
                isServer = false,
                platformOptionsFlags = "None",
                authScopeOptionsFlags = "BasicProfile, FriendsList, Presence",
                integratedPlatformManagementFlags = "Disabled",
                tickBudgetInMilliseconds = 0,
                taskNetworkTimeoutSeconds = 0.0,
                threadAffinity = null,
                alwaysSendInputToOverlay = false,
                initialButtonDelayForOverlay = 0.0f,
                repeatButtonDelayForOverlay = 0.0f,
                toggleFriendsButtonCombination = "SpecialLeft",
                schemaVersion = "1.0"
            };

            string json = JsonUtility.ToJson(config, true);
            File.WriteAllText(Path.Combine(path, "eos_windows_config.json"), json);
            Debug.Log("[EOSConfigImporter] WindowsConfig criado");
        }

        private static void CreateLegacyConfig(string path, EOSCredentialsData creds)
        {
            var config = new LegacyConfigData
            {
                deploymentID = creds.DeploymentId,
                clientID = creds.ClientId,
                tickBudgetInMilliseconds = 0,
                taskNetworkTimeoutSeconds = 0.0,
                platformOptionsFlags = "None",
                authScopeOptionsFlags = "BasicProfile, FriendsList, Presence",
                integratedPlatformManagementFlags = 0,
                alwaysSendInputToOverlay = false,
                schemaVersion = "1.0"
            };

            string json = JsonUtility.ToJson(config, true);
            File.WriteAllText(Path.Combine(path, "EpicOnlineServicesConfig.json"), json);
            Debug.Log("[EOSConfigImporter] LegacyConfig criado");
        }

        // Data classes para serialização
        [System.Serializable]
        private class EOSCredentialsData
        {
            public string ProductId;
            public string SandboxId;
            public string DeploymentId;
            public string ClientId;
            public string ClientSecret;
            public string EncryptionKey;
        }

        [System.Serializable]
        private class ProductConfigData
        {
            public string ProductName;
            public string ProductId;
            public string ProductVersion;
            public bool imported;
            public ClientData[] Clients;
            public EnvironmentsData Environments;
            public string schemaVersion;
        }

        [System.Serializable]
        private class ClientData
        {
            public string Name;
            public ClientValueData Value;
        }

        [System.Serializable]
        private class ClientValueData
        {
            public string ClientId;
            public string ClientSecret;
        }

        [System.Serializable]
        private class EnvironmentsData
        {
            public DeploymentData[] Deployments;
            public SandboxData[] Sandboxes;
        }

        [System.Serializable]
        private class DeploymentData
        {
            public string Name;
            public DeploymentValueData Value;
        }

        [System.Serializable]
        private class DeploymentValueData
        {
            public SandboxIdData SandboxId;
            public string DeploymentId;
        }

        [System.Serializable]
        private class SandboxIdData
        {
            public string Value;
        }

        [System.Serializable]
        private class SandboxData
        {
            public string Name;
            public string Value;
        }

        [System.Serializable]
        private class WindowsConfigData
        {
            public DeploymentValueData deployment;
            public ClientValueData clientCredentials;
            public bool isServer;
            public string platformOptionsFlags;
            public string authScopeOptionsFlags;
            public string integratedPlatformManagementFlags;
            public int tickBudgetInMilliseconds;
            public double taskNetworkTimeoutSeconds;
            public object threadAffinity;
            public bool alwaysSendInputToOverlay;
            public float initialButtonDelayForOverlay;
            public float repeatButtonDelayForOverlay;
            public string toggleFriendsButtonCombination;
            public string schemaVersion;
        }

        [System.Serializable]
        private class LegacyConfigData
        {
            public string deploymentID;
            public string clientID;
            public int tickBudgetInMilliseconds;
            public double taskNetworkTimeoutSeconds;
            public string platformOptionsFlags;
            public string authScopeOptionsFlags;
            public int integratedPlatformManagementFlags;
            public bool alwaysSendInputToOverlay;
            public string schemaVersion;
        }
    }
}
