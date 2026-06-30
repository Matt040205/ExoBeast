#if FMOD_PRESENT
using FMODUnity;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FmodMultiplayerSettings))]
public class FmodMultiplayerSettingsEditor : Editor
{
    private SerializedProperty isMultiplayer;

    private void OnEnable()
    {
        isMultiplayer = serializedObject.FindProperty("isMultiplayer");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(isMultiplayer, new GUIContent("IsMultiplayer"));

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();

            if (isMultiplayer.boolValue)
            {
                ApplyMultiplayerFmodSettings();
            }

            return;
        }

        serializedObject.ApplyModifiedProperties();

        if (isMultiplayer.boolValue && GUILayout.Button("Apply FMOD Multiplayer Settings"))
        {
            ApplyMultiplayerFmodSettings();
        }
    }

    private static void ApplyMultiplayerFmodSettings()
    {
        Settings settings = Settings.Instance;
        if (settings == null)
            return;

        Undo.RecordObject(settings, "Apply FMOD Multiplayer Settings");

        SerializedObject serializedSettings = new SerializedObject(settings);
        serializedSettings.FindProperty("HasSourceProject").boolValue = false;
        serializedSettings.FindProperty("BankRefreshCooldown").intValue = -2;
        serializedSettings.FindProperty("ShowBankRefreshWindow").boolValue = false;
        serializedSettings.ApplyModifiedProperties();

        DisableLiveUpdate(settings.DefaultPlatform);
        DisableLiveUpdate(settings.PlayInEditorPlatform);

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    private static void DisableLiveUpdate(Platform platform)
    {
        if (platform == null)
            return;

        Undo.RecordObject(platform, "Apply FMOD Multiplayer Settings");

        SerializedObject serializedPlatform = new SerializedObject(platform);
        SerializedProperty liveUpdate = serializedPlatform.FindProperty("Properties.LiveUpdate");
        if (liveUpdate != null)
        {
            SerializedProperty value = liveUpdate.FindPropertyRelative("Value");
            if (value.propertyType == SerializedPropertyType.Enum)
                value.enumValueIndex = (int)TriStateBool.Disabled;
            else
                value.intValue = (int)TriStateBool.Disabled;

            liveUpdate.FindPropertyRelative("HasValue").boolValue = true;
        }

        serializedPlatform.ApplyModifiedProperties();
        EditorUtility.SetDirty(platform);
    }
}
#endif
