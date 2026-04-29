using UnityEngine;

public class AllyShieldVisual : MonoBehaviour
{
    public GameObject shieldPrefab;
    private GameObject currentVfx;

    public void SetActive(bool isActive)
    {
        if (isActive && currentVfx == null && shieldPrefab != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 1f;
            currentVfx = Instantiate(shieldPrefab, spawnPos, transform.rotation, transform);

            // Força todos os sistemas de partículas do prefab a rodarem em loop infinito
            ParticleSystem[] pSystems = currentVfx.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in pSystems)
            {
                var main = ps.main;
                main.loop = true;
                main.stopAction = ParticleSystemStopAction.None;
                if (!ps.isPlaying)
                {
                    ps.Play();
                }
            }
        }
        else if (!isActive && currentVfx != null)
        {
            Destroy(currentVfx);
            currentVfx = null;
        }
    }

    private void OnDestroy()
    {
        if (currentVfx != null)
        {
            Destroy(currentVfx);
        }
    }
}
