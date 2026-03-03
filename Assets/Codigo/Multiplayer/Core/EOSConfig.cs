using UnityEngine;

namespace ExoBeasts.Multiplayer.Core
{
    /// <summary>
    /// ScriptableObject para configuracao do Epic Online Services
    /// IMPORTANTE: As credenciais reais devem estar em um arquivo externo (EOSCredentials.json)
    /// que NAO deve ser commitado no Git
    /// </summary>
    [CreateAssetMenu(fileName = "EOSConfig", menuName = "Multiplayer/EOS Config")]
    public class EOSConfig : ScriptableObject
    {
        [Header("Identificadores do Produto")]
        [Tooltip("ID do produto no Epic Developer Portal")]
        public string ProductId = "";

        [Tooltip("ID do Sandbox (Development, Staging, Live)")]
        public string SandboxId = "";

        [Tooltip("ID do Deployment")]
        public string DeploymentId = "";

        [Header("Credenciais do Cliente")]
        [Tooltip("Client ID - Sera carregado de arquivo externo")]
        public string ClientId = "";

        [Tooltip("Client Secret - Sera carregado de arquivo externo")]
        public string ClientSecret = "";

        [Header("Configuracoes de Jogo")]
        [Tooltip("Chave de criptografia (64 caracteres hex)")]
        public string EncryptionKey = "";

        [Header("Configuracoes de Arquivo")]
        [Tooltip("Caminho do arquivo de credenciais (relativo ao projeto)")]
        public string credentialsFilePath = "EOSCredentials.json";

        /// <summary>
        /// Carregar credenciais de arquivo externo
        /// </summary>
        public void LoadCredentialsFromFile()
        {
            string filePath = System.IO.Path.Combine(Application.dataPath, "..", credentialsFilePath);

            if (!System.IO.File.Exists(filePath))
            {
                Debug.LogError($"[EOSConfig] Arquivo de credenciais nao encontrado: {filePath}");
                Debug.LogError("[EOSConfig] Crie o arquivo EOSCredentials.json na raiz do projeto!");
                return;
            }

            try
            {
                string json = System.IO.File.ReadAllText(filePath);
                EOSCredentials credentials = JsonUtility.FromJson<EOSCredentials>(json);

                // Atribuir valores (sem mostrar no log)
                ProductId = credentials.ProductId;
                SandboxId = credentials.SandboxId;
                DeploymentId = credentials.DeploymentId;
                ClientId = credentials.ClientId;
                ClientSecret = credentials.ClientSecret;
                EncryptionKey = credentials.EncryptionKey;

                Debug.Log("[EOSConfig] Credenciais carregadas com sucesso!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[EOSConfig] Erro ao carregar credenciais: {e.Message}");
            }
        }

        /// <summary>
        /// Validar se as credenciais estao configuradas
        /// </summary>
        public bool ValidateCredentials()
        {
            bool isValid = !string.IsNullOrEmpty(ProductId) &&
                          !string.IsNullOrEmpty(SandboxId) &&
                          !string.IsNullOrEmpty(DeploymentId) &&
                          !string.IsNullOrEmpty(ClientId) &&
                          !string.IsNullOrEmpty(ClientSecret);

            if (!isValid)
            {
                Debug.LogError("[EOSConfig] Credenciais incompletas! Execute LoadCredentialsFromFile()");
            }

            return isValid;
        }

        /// <summary>
        /// Limpar credenciais da memoria (seguranca)
        /// </summary>
        public void ClearCredentials()
        {
            ProductId = "";
            SandboxId = "";
            DeploymentId = "";
            ClientId = "";
            ClientSecret = "";
            EncryptionKey = "";
            Debug.Log("[EOSConfig] Credenciais limpas da memoria");
        }
    }

    /// <summary>
    /// Estrutura para deserializar o arquivo JSON de credenciais
    /// </summary>
    [System.Serializable]
    public class EOSCredentials
    {
        public string ProductId;
        public string SandboxId;
        public string DeploymentId;
        public string ClientId;
        public string ClientSecret;
        public string EncryptionKey;
    }
}
