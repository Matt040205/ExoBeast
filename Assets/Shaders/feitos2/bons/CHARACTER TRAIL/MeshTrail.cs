using System.Collections;
using UnityEngine;

public class MeshTrail : MonoBehaviour
{
    [Header("Tempo de Ativação do Dash")]
    public float activeTime = 0.3f;

    [Header("Configurações do Mesh")]
    public float meshRefreshRate = 0.05f;
    public float meshDestroyDelay = 2f;
    public Transform positionToSpawn;

    [Header("Configurações do Shader")]
    public Material mat;
    public string shaderVarRef = "_Alpha";
    public float shaderVarRate = 0.1f;
    public float shaderVarRefreshRate = 0.05f;

    private bool isTrailActive;
    private SkinnedMeshRenderer[] skinnedMeshRenderers;

    void Start()
    {
        if (positionToSpawn == null)
        {
            positionToSpawn = this.transform;
        }
    }

    // Função que é ativada pelo script de Movimento (TPSMovementAndCamera)
    public void TriggerTrail()
    {
        if (!isTrailActive)
        {
            isTrailActive = true;
            StartCoroutine(ActivateTrail(activeTime));
        }
    }

    IEnumerator ActivateTrail(float timeActive)
    {
        while (timeActive > 0)
        {
            timeActive -= meshRefreshRate;

            if (skinnedMeshRenderers == null || skinnedMeshRenderers.Length == 0)
            {
                skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();

                // SISTEMA DE AVISO: Se não encontrar o modelo 3D, avisa no Console
                if (skinnedMeshRenderers.Length == 0)
                {
                    Debug.LogWarning("ALERTA: O MeshTrail não encontrou nenhum 'SkinnedMeshRenderer' nos filhos! Você está testando com uma Cápsula do Unity? O efeito precisa da malha 3D real da sua personagem para funcionar.");
                }
            }

            for (int i = 0; i < skinnedMeshRenderers.Length; i++)
            {
                // Cria o Clone
                GameObject gObj = new GameObject("TrailMesh");
                gObj.transform.SetPositionAndRotation(positionToSpawn.position, positionToSpawn.rotation);

                MeshRenderer mr = gObj.AddComponent<MeshRenderer>();
                MeshFilter mf = gObj.AddComponent<MeshFilter>();

                Mesh mesh = new Mesh();
                skinnedMeshRenderers[i].BakeMesh(mesh);
                mf.mesh = mesh;

                // Aplica o material do Shader Graph
                if (mat != null)
                {
                    mr.material = mat;
                    StartCoroutine(AnimateMaterialFloat(mr.material, 0f, shaderVarRate, shaderVarRefreshRate));
                }
                else
                {
                    Debug.LogWarning("ALERTA: Nenhum material associado no script MeshTrail!");
                }

                Destroy(gObj, meshDestroyDelay);
            }

            yield return new WaitForSeconds(meshRefreshRate);
        }

        isTrailActive = false;
    }

    IEnumerator AnimateMaterialFloat(Material material, float goal, float rate, float refreshRate)
    {
        float valueToAnimate = 1f;
        if (material.HasProperty(shaderVarRef))
        {
            valueToAnimate = material.GetFloat(shaderVarRef);
        }
        else
        {
            Debug.LogWarning("ALERTA: O material não tem a propriedade " + shaderVarRef + ". Veja se escreveu certo no Shader Graph!");
        }

        while (valueToAnimate > goal)
        {
            valueToAnimate -= rate;
            material.SetFloat(shaderVarRef, valueToAnimate);
            yield return new WaitForSeconds(refreshRate);
        }
    }
}