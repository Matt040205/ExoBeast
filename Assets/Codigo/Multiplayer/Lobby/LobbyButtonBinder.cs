using System;
using UnityEngine;
using UnityEngine.UI;

namespace ExoBeasts.Multiplayer.Lobby
{
    public static class LobbyButtonBinder
    {
        public static void WireBtn(MonoBehaviour mono, string goName, Action handler)
        {
            foreach (var b in mono.GetComponentsInChildren<Button>(true))
            {
                if (b.gameObject.name.Trim() != goName.Trim()) continue;
                // Cria um novo evento limpando tudo o que possa estar erradamente "injetado" no Inspecionar!
                b.onClick = new Button.ButtonClickedEvent();
                b.onClick.AddListener(() => handler());
            }
        }

        public static void WireBtnByPath(MonoBehaviour mono, string path, Action handler)
        {
            var tr = mono.transform.Find(path);
            if (tr != null)
            {
                Debug.Log($"[LobbyButtonBinder] SUCESSO ao encontrar e mapear o botao via path: {path}");
                var b = tr.GetComponent<Button>();
                if (b != null)
                {
                    b.onClick = new Button.ButtonClickedEvent();
                    b.onClick.AddListener(() => handler());
                }
            }
            else
            {
                Debug.LogError($"[LobbyButtonBinder] FALHA FATAL: Nao achou o botao no path '{path}'");
            }
        }

        public static void WireBtnInParent(MonoBehaviour mono, string parentName, string btnName, Action handler)
        {
            var parent = FindGO(mono, parentName);
            if (parent == null) return;
            foreach (var b in parent.GetComponentsInChildren<Button>(true))
            {
                if (b.gameObject.name.Trim() != btnName.Trim()) continue;
                b.onClick = new Button.ButtonClickedEvent();
                b.onClick.AddListener(() => handler());
            }
        }

        private static GameObject FindGO(MonoBehaviour mono, string goName)
        {
            foreach (var t in mono.GetComponentsInChildren<Transform>(true))
                if (t.gameObject.name.Trim() == goName.Trim()) return t.gameObject;
            return null;
        }
    }
}
