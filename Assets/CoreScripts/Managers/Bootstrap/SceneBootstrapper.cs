using UnityEngine;
using UnityEngine.SceneManagement;

namespace ExoBeasts.Managers.Bootstrap
{
    public sealed class SceneBootstrapper : MonoBehaviour
    {
        [SerializeField] private string initialSceneName = "MenuScene";

        private void Start()
        {
            if (string.IsNullOrWhiteSpace(initialSceneName))
            {
                Debug.LogError("[SceneBootstrapper] initialSceneName vazio. Bootstrap abortado.");
                return;
            }

            SceneManager.LoadScene(initialSceneName, LoadSceneMode.Single);
        }
    }
}
