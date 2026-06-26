#if UNITY_EDITOR
using UnityEditor;
using Unity.Netcode;
using UnityEngine;

namespace ExoBeasts.Multiplayer.Core
{
    /// <summary>
    /// Resolve o bug do Unity MPPM onde clones falham ao inicializar a NetworkPrefabsList
    /// deixando prefabs com GlobalObjectIdHash = 0.
    /// Este script roda ANTES do NetworkManager inicializar e silencia/remove os prefabs corrompidos 
    /// apenas no clone, utilizando uma copia em memoria para nao alterar o asset do projeto.
    /// </summary>
    public static class MppmPrefabFix
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ApplyCloneFix()
        {
            if (!MppmHelper.IsClone) return;

            var networkManagers = Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var nm in networkManagers)
            {
                if (nm.NetworkConfig != null && nm.NetworkConfig.Prefabs != null)
                {
                    var oldLists = new System.Collections.Generic.List<NetworkPrefabsList>(nm.NetworkConfig.Prefabs.NetworkPrefabsLists);
                    nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();

                    foreach (var listAsset in oldLists)
                    {
                        if (listAsset != null)
                        {
                            var cloneList = Object.Instantiate(listAsset);
                            var prefabsToRemove = new System.Collections.Generic.List<NetworkPrefab>();
                            
                            foreach (var p in cloneList.PrefabList)
                            {
                                if (p.SourcePrefabGlobalObjectIdHash == 0 && p.Prefab == null)
                                {
                                    prefabsToRemove.Add(p);
                                }
                            }

                            foreach (var p in prefabsToRemove)
                            {
                                Debug.LogWarning($"[MppmPrefabFix] Removendo prefab invalido (Hash 0) do clone na lista {cloneList.name}.");
                                cloneList.Remove(p);
                            }

                            nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(cloneList);
                        }
                    }
                }
            }
        }
    }
}
#endif
