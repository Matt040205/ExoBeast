using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshTrail : MonoBehaviour
{
    [Header("Configurações do Mesh")]
    public float meshDestroyDelay = 2f;

    [Header("Configurações do Shader (Shader Graph)")]
    public Material mat;
    public string shaderVarRef = "_Alpha";
    public float shaderVarRate = 0.1f;
    public float shaderVarRefreshRate = 0.05f;

    private SkinnedMeshRenderer[] skinnedMeshRenderers;
    private Queue<GameObject> ghostPool = new Queue<GameObject>();

    void Start()
    {
        skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        if (skinnedMeshRenderers.Length == 0)
        {
            Debug.LogWarning("ALERTA: O MeshTrail não encontrou nenhum 'SkinnedMeshRenderer' nos filhos!");
        }
    }

    /// <summary>
    /// Faz o Bake da malha na posição e rotação solicitadas e aplica o efeito de ghosting.
    /// </summary>
    public void SpawnGhostAt(Vector3 position, Quaternion rotation, Transform modelPivot)
    {
        if (skinnedMeshRenderers == null || skinnedMeshRenderers.Length == 0) return;

        for (int i = 0; i < skinnedMeshRenderers.Length; i++)
        {
            GameObject gObj = GetFromPool();
            
            // A malha gerada em BakeMesh é baseada no espaço local do SkinnedMeshRenderer atual.
            // Posicionamos o clone no novo ponto calculado.
            gObj.transform.SetPositionAndRotation(position, rotation);

            MeshRenderer mr = gObj.GetComponent<MeshRenderer>();
            MeshFilter mf = gObj.GetComponent<MeshFilter>();

            Mesh mesh = new Mesh();
            skinnedMeshRenderers[i].BakeMesh(mesh);
            mf.mesh = mesh;

            if (mat != null)
            {
                // Instancia o material (clone) para que a animação de um ghost não afete os outros
                mr.material = new Material(mat);
                StartCoroutine(AnimateMaterialFloat(mr.material, 0f, shaderVarRate, shaderVarRefreshRate, gObj));
            }
            else
            {
                Debug.LogWarning("ALERTA: Nenhum material associado no script MeshTrail!");
                ReturnToPool(gObj, meshDestroyDelay);
            }
        }
    }

    private GameObject GetFromPool()
    {
        if (ghostPool.Count > 0)
        {
            GameObject obj = ghostPool.Dequeue();
            obj.SetActive(true);
            return obj;
        }

        GameObject gObj = new GameObject("TrailMeshGhost");
        gObj.AddComponent<MeshRenderer>();
        gObj.AddComponent<MeshFilter>();
        return gObj;
    }

    private void ReturnToPool(GameObject gObj, float delay)
    {
        StartCoroutine(ReturnToPoolCoroutine(gObj, delay));
    }

    private IEnumerator ReturnToPoolCoroutine(GameObject gObj, float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        
        gObj.SetActive(false);
        
        MeshFilter mf = gObj.GetComponent<MeshFilter>();
        if (mf != null && mf.mesh != null)
        {
            // Limpa a malha da memória para evitar Memory Leak massivo (GC)
            Destroy(mf.mesh); 
            mf.mesh = null;
        }

        MeshRenderer mr = gObj.GetComponent<MeshRenderer>();
        if (mr != null && mr.material != null)
        {
            // Destroi a instância do material também
            Destroy(mr.material);
        }
        
        ghostPool.Enqueue(gObj);
    }

    IEnumerator AnimateMaterialFloat(Material material, float goal, float rate, float refreshRate, GameObject gObj)
    {
        float valueToAnimate = 1f;
        if (material.HasProperty(shaderVarRef))
        {
            valueToAnimate = material.GetFloat(shaderVarRef);
        }

        while (valueToAnimate > goal)
        {
            valueToAnimate -= rate;
            material.SetFloat(shaderVarRef, valueToAnimate);
            yield return new WaitForSeconds(refreshRate);
        }
        
        ReturnToPool(gObj, 0f);
    }

    [System.Obsolete("TriggerTrail is deprecated. Use SpawnGhostAt for precise distance-based clones.")]
    public void TriggerTrail()
    {
        Debug.LogWarning("MeshTrail.TriggerTrail() foi chamado por um script antigo, mas este método está obsoleto e foi desativado em favor do SpawnGhostAt.");
    }
}