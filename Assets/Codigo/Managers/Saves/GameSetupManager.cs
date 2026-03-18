using UnityEngine;
using Unity.Netcode;

public class GameSetupManager : MonoBehaviour
{
    public Transform spawnPoint;

    void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                SetupGame();
            }
            else
            {
                NetworkManager.Singleton.OnServerStarted += SetupGame;
            }
        }
    }

    private void SetupGame()
    {
        if (GameDataManager.Instance == null) return;

        CharacterBase commanderData = GameDataManager.Instance.equipeSelecionada[0];

        if (commanderData != null && commanderData.commanderPrefab != null)
        {
            Vector3 pos = new Vector3(0, 50f, 0);
            Quaternion rot = Quaternion.identity;

            if (spawnPoint != null)
            {
                pos = spawnPoint.position;
                rot = spawnPoint.rotation;
            }
            else
            {
                GameObject spawnObj = GameObject.Find("RespawnPoint");
                if (spawnObj == null) spawnObj = GameObject.FindGameObjectWithTag("Respawn");

                if (spawnObj != null)
                {
                    pos = spawnObj.transform.position;
                    rot = spawnObj.transform.rotation;
                }
            }

            GameObject playerInstance = Instantiate(commanderData.commanderPrefab, pos, rot);

            NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.SpawnAsPlayerObject(NetworkManager.Singleton.LocalClientId);
            }
        }

        if (BuildManager.Instance != null && GameDataManager.Instance != null)
        {
            BuildManager.Instance.SetAvailableTowers(GameDataManager.Instance.equipeSelecionada);
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= SetupGame;
        }
    }
}