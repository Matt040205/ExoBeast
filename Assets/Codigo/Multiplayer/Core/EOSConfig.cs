using UnityEngine;

namespace ExoBeasts.Multiplayer.Core
{
    /// <summary>
    /// ── EOSConfig ────────────────────────────────────────
    /// ScriptableObject com credenciais e configuracoes do EOS.
    ///
    ///  ▸ Credenciais carregadas de EOSCredentials.json (externo ao Git)
    ///  ▸ LoadCredentialsFromFile(): popula campos em runtime
    ///  ▸ ValidateCredentials(): verifica campos obrigatorios
    ///  ▸ ClearCredentials(): limpa da memoria ao encerrar
    ///  ▸ Criar via: Assets > Create > Multiplayer > EOS Config
    /// ─────────────────────────────────────────────────────
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

        public void LoadCredentialsFromFile()
        {
            // Clones MPPM têm Application.dataPath apontando para Library/VP/{vpId}/Assets
            // em vez da raiz real do projeto. Precisamos subir 4 níveis para chegar em PI3D/.
            string dataParent = MppmHelper.IsClone
                ? System.IO.Path.Combine(Application.dataPath, "..", "..", "..", "..")
                : System.IO.Path.Combine(Application.dataPath, "..");

            string filePath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(dataParent, credentialsFilePath));

            // Fallback 1: StreamingAssets (funciona em builds se o arquivo foi copiado)
            if (!System.IO.File.Exists(filePath))
            {
                string streamingPath = System.IO.Path.Combine(
                    Application.streamingAssetsPath, credentialsFilePath);
                if (System.IO.File.Exists(streamingPath))
                {
                    filePath = streamingPath;
                    Debug.Log($"[EOSConfig] Usando credenciais de StreamingAssets: {filePath}");
                }
            }

            if (!System.IO.File.Exists(filePath))
            {
                // Fallback 2: usar credenciais já serializadas no ScriptableObject (baked no Inspector)
                if (ValidateCredentials())
                {
                    Debug.Log("[EOSConfig] Arquivo externo nao encontrado, usando credenciais baked no ScriptableObject.");
                    return;
                }

                Debug.LogError($"[EOSConfig] Arquivo de credenciais nao encontrado: {filePath}");
                Debug.LogError("[EOSConfig] Crie o arquivo EOSCredentials.json na raiz do projeto, " +
                               "ou preencha os campos diretamente no Inspector do EOSConfig asset!");
                return;
            }

            try
            {
                string json = System.IO.File.ReadAllText(filePath);
                EOSCredentials credentials = JsonUtility.FromJson<EOSCredentials>(json);

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
                // Fallback: se o parse falhou mas temos credenciais baked, usa elas
                if (ValidateCredentials())
                    Debug.LogWarning("[EOSConfig] Usando credenciais baked como fallback apos erro de parse.");
            }
        }

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
