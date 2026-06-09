using UnityEngine;

/// <summary>
/// Detecta qual textura do Terrain está dominante sob o transform deste GameObject.
/// Adicione ao mesmo objeto do Player. Configure os índices das layers no Inspector
/// para corresponder à ordem das Terrain Layers do seu Terrain.
/// </summary>
public class TerrainSurfaceDetector : MonoBehaviour
{
    public enum SurfaceType { Terra, Concreto, Agua, Desconhecido }

    [Header("Índices das Terrain Layers")]
    [Tooltip("Índice da layer de Terra no Terrain (começa em 0).")]
    public int terraLayerIndex = 0;
    [Tooltip("Índice da layer de Concreto no Terrain.")]
    public int concretoLayerIndex = 1;
    [Tooltip("Índice da layer de Água no Terrain.")]
    public int aguaLayerIndex = 2;

    [Header("Performance")]
    [Tooltip("Intervalo em segundos entre cada checagem de superfície. Valores menores = mais preciso mas mais pesado.")]
    public float checkInterval = 0.2f;

    private Terrain cachedTerrain;
    private SurfaceType currentSurface = SurfaceType.Terra;
    private float nextCheckTime;

    /// <summary>
    /// Retorna o tipo de superfície atual sob o player.
    /// </summary>
    public SurfaceType CurrentSurface => currentSurface;

    private void Start()
    {
        cachedTerrain = Terrain.activeTerrain;
        if (cachedTerrain == null)
        {
            Debug.LogWarning("[TerrainSurfaceDetector] Nenhum Terrain ativo encontrado na cena.");
        }
    }

    private void Update()
    {
        if (Time.time < nextCheckTime) return;
        nextCheckTime = Time.time + checkInterval;

        if (cachedTerrain == null)
        {
            cachedTerrain = Terrain.activeTerrain;
            if (cachedTerrain == null) return;
        }

        currentSurface = DetectSurface(transform.position);
    }

    private SurfaceType DetectSurface(Vector3 worldPos)
    {
        TerrainData terrainData = cachedTerrain.terrainData;
        Vector3 terrainPos = cachedTerrain.transform.position;

        // Converte posição do mundo para coordenadas normalizadas do terrain (0-1)
        float normalizedX = (worldPos.x - terrainPos.x) / terrainData.size.x;
        float normalizedZ = (worldPos.z - terrainPos.z) / terrainData.size.z;

        // Clamp para garantir que estamos dentro dos limites
        normalizedX = Mathf.Clamp01(normalizedX);
        normalizedZ = Mathf.Clamp01(normalizedZ);

        // Converte para coordenadas do alphamap
        int mapX = Mathf.RoundToInt(normalizedX * (terrainData.alphamapWidth - 1));
        int mapZ = Mathf.RoundToInt(normalizedZ * (terrainData.alphamapHeight - 1));

        // Lê os pesos de todas as texturas neste ponto (1x1 pixel)
        float[,,] alphamap = terrainData.GetAlphamaps(mapX, mapZ, 1, 1);
        int layerCount = alphamap.GetLength(2);

        // Encontra a layer com maior peso
        int dominantIndex = 0;
        float maxWeight = 0f;
        for (int i = 0; i < layerCount; i++)
        {
            if (alphamap[0, 0, i] > maxWeight)
            {
                maxWeight = alphamap[0, 0, i];
                dominantIndex = i;
            }
        }

        // Mapeia o índice da layer para o tipo de superfície
        if (dominantIndex == terraLayerIndex) return SurfaceType.Terra;
        if (dominantIndex == concretoLayerIndex) return SurfaceType.Concreto;
        if (dominantIndex == aguaLayerIndex) return SurfaceType.Agua;

        return SurfaceType.Desconhecido;
    }
}
