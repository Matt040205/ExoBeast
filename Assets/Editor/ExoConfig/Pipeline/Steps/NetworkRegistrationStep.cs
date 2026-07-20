using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using ExoBeasts.ExoConfig.Core;

/// <summary>
/// Fase 7 da refatoracao Exo Config. Registra o(s) prefab(s) montados nesta
/// execucao (context.BuiltPrefabPaths - Personagem+Torre ou so Monstro) em
/// Assets/Multiplayer/Setup/DefaultNetworkPrefabs.asset, sem duplicar.
///
/// Substitui o antigo aviso manual de ExoPrefabBuilder.BuildCharacterPrefab:
///   Debug.LogWarning("[ExoConfig] ACAO NECESSARIA: Arraste os prefabs para
///   Assets/Multiplayer/Setup/DefaultNetworkPrefabs.asset para funcionar em
///   rede.");
/// que rodava sob a condicao "entityType != ExoEntityType.Edificio" - MESMA
/// condicao de guarda usada abaixo (Execute, primeiro if), confirmada antes
/// de remover o aviso original (ver ExoPrefabBuilder.cs).
///
/// Caminho do asset FIXO (nao um AssetDatabase.FindAssets por tipo/nome):
/// confirmado nesta fase, via grep do guid dentro de Assets/Cenas/MenuScene.unity,
/// que "Assets/Multiplayer/Setup/DefaultNetworkPrefabs.asset" (guid
/// 7268e1b54a188824fb4131b18a19e867) e o NetworkPrefabsList referenciado de
/// verdade pelo NetworkManager da cena. "Assets/DefaultNetworkPrefabs.asset"
/// (raiz do projeto, guid 5026230e68b007f4ea59a2e989f4a0c5) e orfao - zero
/// referencias em qualquer cena/ScriptableObject do projeto - e
/// deliberadamente IGNORADO por este step (mesmo raciocinio de
/// ExoToolConfig.AssetPath: caminho fixo e conhecido evita a ambiguidade de
/// "qual dos dois" que um FindAssets teria).
///
/// So usa a API PUBLICA de Unity.Netcode.NetworkPrefabsList
/// (Contains(GameObject)/Add(NetworkPrefab) - ver
/// Library/PackageCache/com.unity.netcode.gameobjects@.../Runtime/Configuration/NetworkPrefabsList.cs,
/// lido nesta fase para confirmar os nomes exatos antes de escrever este
/// arquivo): o campo serializado "List" e "internal" ao assembly do pacote,
/// sem acesso daqui, mas nao precisa - Contains/Add sao exatamente a
/// superficie publica que o pacote oferece para este proposito, e Contains(GameObject)
/// compara por referencia (List[i].Prefab == prefab), o que basta para
/// "mesmo prefab" (assets carregados pelo mesmo GUID via AssetDatabase
/// resolvem para a MESMA instancia de Object dentro de uma sessao de
/// Editor).
/// </summary>
public sealed class NetworkRegistrationStep : IExoBuildStep
{
    /// <summary>Ver comentario da classe para a prova de que este e o caminho certo (nao o orfao na raiz).</summary>
    internal const string NetworkPrefabsListPath = "Assets/Multiplayer/Setup/DefaultNetworkPrefabs.asset";

    public string Name => "NetworkRegistration";

    public void Execute(ExoBuildContext context)
    {
        if (context.EntityType == ExoEntityType.Edificio)
        {
            context.Report.Info("Edificio nao usa NetworkObject - pulando registro de rede.", context.Nome);
            return;
        }

        if (context.DryRun)
        {
            context.Report.Info(
                "[DryRun] Prefab(s) de \"" + context.Nome + "\" seriam registrados em \"" + NetworkPrefabsListPath + "\" (se ainda nao estiverem).",
                context.Nome);
            return;
        }

        if (context.BuiltPrefabPaths.Count == 0)
        {
            context.Report.Info("Nenhum prefab foi montado nesta execucao - nada para registrar em rede.", context.Nome);
            return;
        }

        NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsListPath);
        if (list == null)
        {
            context.Report.Error("NetworkPrefabsList nao encontrado em \"" + NetworkPrefabsListPath + "\" - nao foi possivel registrar os prefabs para rede.", context.Nome);
            return;
        }

        bool changed = false;
        foreach (string prefabPath in context.BuiltPrefabPaths)
            changed |= RegisterPrefab(context, list, prefabPath);

        if (changed)
        {
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
        }
    }

    /// <summary>Devolve true se registrou uma entrada NOVA (para o chamador decidir se precisa salvar o asset).</summary>
    private static bool RegisterPrefab(ExoBuildContext context, NetworkPrefabsList list, string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            context.Report.Warning("Prefab \"" + prefabPath + "\" nao pode ser carregado para registro de rede.", context.Nome);
            return false;
        }

        // Defesa extra (nao so confiar no chamador filtrar Edificio): so
        // registra o que de fato tem NetworkObject. Nao deveria acontecer
        // hoje para Personagem/Torre/Monstro (ConfigureAsTower/ConfigureAsEnemy/
        // o basePrefab do Personagem sempre adicionam NetworkObject - ver
        // ExoPrefabBuilder), mas evita poluir a lista de rede com um prefab
        // nao-networked em silencio se essa premissa mudar no futuro.
        if (prefab.GetComponent<NetworkObject>() == null)
        {
            context.Report.Warning("Prefab \"" + prefabPath + "\" nao tem NetworkObject - nao foi registrado em \"" + NetworkPrefabsListPath + "\".", context.Nome);
            return false;
        }

        if (list.Contains(prefab))
        {
            context.Report.Info("Prefab \"" + prefabPath + "\" ja estava registrado em \"" + NetworkPrefabsListPath + "\".", context.Nome);
            return false;
        }

        list.Add(new NetworkPrefab { Prefab = prefab });
        context.Report.Info("Prefab \"" + prefabPath + "\" registrado em \"" + NetworkPrefabsListPath + "\".", context.Nome);
        return true;
    }
}
