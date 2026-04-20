using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

namespace ExoBeasts.Multiplayer.Editor
{
    /// <summary>
    /// Garante que o arquivo EOSCredentials.json e as credenciais internas 
    /// sejam sincronizados com o diretório StreamingAssets antes de cada Exportação (Build).
    /// Evita a anomalia do jogo rodar impecavelmente no Editor e falhar autenticacao na Build.
    /// </summary>
    public class EOSBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            string credentialsSource = Path.Combine(Application.dataPath, "..", "EOSCredentials.json");
            string streamingAssetsDir = Application.streamingAssetsPath;
            string credentialsDest = Path.Combine(streamingAssetsDir, "EOSCredentials.json");

            if (File.Exists(credentialsSource))
            {
                if (!Directory.Exists(streamingAssetsDir))
                {
                    Directory.CreateDirectory(streamingAssetsDir);
                }

                // Copia do raw JSON force-override
                File.Copy(credentialsSource, credentialsDest, true);
                Debug.Log("[EOSBuildProcessor] Sucesso: 'EOSCredentials.json' injetado automagicamente na raiz do StreamingAssets(Build).");

                // Aproveita e também dispara o Importer do PlayEveryWare (Ponte de DLL nativo)
                if (System.Type.GetType("ExoBeasts.Multiplayer.Editor.EOSConfigImporter") != null)
                {
                    EOSConfigImporter.ImportCredentials();
                }
            }
            else
            {
                Debug.LogWarning("[EOSBuildProcessor] Cuidado: 'EOSCredentials.json' nao encontrado na raiz. O Build usara as ultimas credenciais Baked na Engine ou falhara o EOS!");
            }
            
            AssetDatabase.Refresh();
        }
    }
}
