using UnityEngine;
using System;
using System.IO;

namespace ExoBeasts.Multiplayer.Core
{
    [CreateAssetMenu(fileName = "EOSConfig", menuName = "Multiplayer/EOS Config")]
    public class EOSConfig : ScriptableObject
    {
        [Header("Identificadores do Produto")]
        [Tooltip("Carregado em runtime — nao baked no asset")]
        [NonSerialized] public string ProductId = "";

        [NonSerialized] public string SandboxId = "";
        [NonSerialized] public string DeploymentId = "";

        [Header("Credenciais do Cliente")]
        [NonSerialized] public string ClientId = "";
        [NonSerialized] public string ClientSecret = "";

        [Header("Configuracoes de Jogo")]
        [NonSerialized] public string EncryptionKey = "";

        private const string CREDENTIALS_FILE = "EOSCredentials.json";

        public void LoadCredentials()
        {
            if (TryLoadFromEnvironment())
            {
                Debug.Log("[EOSConfig] Credenciais carregadas de variaveis de ambiente.");
                return;
            }

            if (TryLoadFromFile())
            {
                Debug.Log("[EOSConfig] Credenciais carregadas de EOSCredentials.json.");
                return;
            }

            if (TryLoadFromStreamingAssets())
            {
                Debug.Log("[EOSConfig] Credenciais carregadas de StreamingAssets/EOS/.");
                return;
            }

            Debug.LogError(
                "[EOSConfig] Nenhuma fonte de credenciais encontrada!\n" +
                "  Opcao A: defina variaveis de ambiente (EOS_PRODUCT_ID, EOS_CLIENT_ID, etc.)\n" +
                "  Opcao B: crie EOSCredentials.json na raiz do projeto\n" +
                "  Opcao C: gere configs via menu Tools > ExoBeasts > Generate EOS Config");
        }

        private bool TryLoadFromEnvironment()
        {
            string productId = Environment.GetEnvironmentVariable("EOS_PRODUCT_ID");
            string clientId = Environment.GetEnvironmentVariable("EOS_CLIENT_ID");

            if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(clientId))
                return false;

            ProductId = productId;
            SandboxId = Environment.GetEnvironmentVariable("EOS_SANDBOX_ID") ?? "";
            DeploymentId = Environment.GetEnvironmentVariable("EOS_DEPLOYMENT_ID") ?? "";
            ClientId = clientId;
            ClientSecret = Environment.GetEnvironmentVariable("EOS_CLIENT_SECRET") ?? "";
            EncryptionKey = Environment.GetEnvironmentVariable("EOS_ENCRYPTION_KEY") ?? "";
            return true;
        }

        private bool TryLoadFromFile()
        {
            string dataParent = MppmHelper.IsClone
                ? Path.Combine(Application.dataPath, "..", "..", "..", "..")
                : Path.Combine(Application.dataPath, "..");

            string filePath = Path.GetFullPath(Path.Combine(dataParent, CREDENTIALS_FILE));

            if (!File.Exists(filePath))
                return false;

            return ParseCredentialsFile(filePath);
        }

        private bool TryLoadFromStreamingAssets()
        {
            string eosDir = Path.Combine(Application.streamingAssetsPath, "EOS");
            string productConfigPath = Path.Combine(eosDir, "eos_product_config.json");
            string windowsConfigPath = Path.Combine(eosDir, "eos_windows_config.json");

            if (!File.Exists(productConfigPath) || !File.Exists(windowsConfigPath))
                return false;

            try
            {
                string productJson = File.ReadAllText(productConfigPath);
                var productData = JsonUtility.FromJson<ProductConfigMinimal>(productJson);

                ProductId = productData.ProductId ?? "";

                if (productData.Clients != null && productData.Clients.Length > 0)
                {
                    ClientId = productData.Clients[0].Value?.ClientId ?? "";
                    ClientSecret = productData.Clients[0].Value?.ClientSecret ?? "";
                    EncryptionKey = productData.Clients[0].Value?.GetEncryptionKey() ?? "";
                }

                string windowsJson = File.ReadAllText(windowsConfigPath);
                var windowsData = JsonUtility.FromJson<WindowsConfigMinimal>(windowsJson);

                SandboxId = windowsData.deployment?.SandboxId?.Value ?? "";
                DeploymentId = windowsData.deployment?.DeploymentId ?? "";
                string windowsEncryptionKey = windowsData.clientCredentials?.GetEncryptionKey() ?? "";
                if (!string.IsNullOrEmpty(windowsEncryptionKey))
                {
                    EncryptionKey = windowsEncryptionKey;
                }

                return ValidateCredentials(silent: true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EOSConfig] Erro ao ler StreamingAssets/EOS: {e.Message}");
                return false;
            }
        }

        private bool ParseCredentialsFile(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var credentials = JsonUtility.FromJson<EOSCredentials>(json);

                ProductId = credentials.ProductId ?? "";
                SandboxId = credentials.SandboxId ?? "";
                DeploymentId = credentials.DeploymentId ?? "";
                ClientId = credentials.ClientId ?? "";
                ClientSecret = credentials.ClientSecret ?? "";
                EncryptionKey = credentials.EncryptionKey ?? "";
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EOSConfig] Erro ao ler credenciais: {e.Message}");
                return false;
            }
        }

        public bool ValidateCredentials(bool silent = false)
        {
            bool isValid = !string.IsNullOrEmpty(ProductId) &&
                          !string.IsNullOrEmpty(SandboxId) &&
                          !string.IsNullOrEmpty(DeploymentId) &&
                          !string.IsNullOrEmpty(ClientId) &&
                          !string.IsNullOrEmpty(ClientSecret) &&
                          IsValidEncryptionKey(EncryptionKey);

            if (!isValid && !silent)
            {
                Debug.LogError("[EOSConfig] Credenciais incompletas! Verifique a fonte de credenciais.");
            }

            return isValid;
        }

        public void ClearCredentials()
        {
            ProductId = "";
            SandboxId = "";
            DeploymentId = "";
            ClientId = "";
            ClientSecret = "";
            EncryptionKey = "";
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
    }

    [Serializable]
    public class EOSCredentials
    {
        public string ProductId;
        public string SandboxId;
        public string DeploymentId;
        public string ClientId;
        public string ClientSecret;
        public string EncryptionKey;
    }

    [Serializable]
    internal class ProductConfigMinimal
    {
        public string ProductId;
        public ClientEntry[] Clients;

        [Serializable]
        internal class ClientEntry
        {
            public string Name;
            public ClientValue Value;
        }

        [Serializable]
        internal class ClientValue
        {
            public string ClientId;
            public string ClientSecret;
            public string EncryptionKey;
            public string encryptionKey;

            public string GetEncryptionKey()
            {
                return !string.IsNullOrEmpty(EncryptionKey) ? EncryptionKey : encryptionKey;
            }
        }
    }

    [Serializable]
    internal class WindowsConfigMinimal
    {
        public DeploymentEntry deployment;
        public ClientCredentialsEntry clientCredentials;

        [Serializable]
        internal class DeploymentEntry
        {
            public SandboxIdEntry SandboxId;
            public string DeploymentId;
        }

        [Serializable]
        internal class SandboxIdEntry
        {
            public string Value;
        }

        [Serializable]
        internal class ClientCredentialsEntry
        {
            public string EncryptionKey;
            public string encryptionKey;

            public string GetEncryptionKey()
            {
                return !string.IsNullOrEmpty(EncryptionKey) ? EncryptionKey : encryptionKey;
            }
        }
    }
}
