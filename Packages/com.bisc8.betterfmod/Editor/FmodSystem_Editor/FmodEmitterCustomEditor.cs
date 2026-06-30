#if FMOD_PRESENT
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FmodEmitterCustom))]
public class FmodEmitterCustomEditor : Editor
{
    SerializedProperty mode;
    SerializedProperty eventId;
    SerializedProperty is3D;
    SerializedProperty oneShot;
    SerializedProperty playEvent;
    SerializedProperty stopEvent;
    SerializedProperty radius;
    SerializedProperty gizmoColor;

    void OnEnable()
    {
        mode = serializedObject.FindProperty("mode");
        eventId = serializedObject.FindProperty("eventId");
        is3D = serializedObject.FindProperty("is3D");
        oneShot = serializedObject.FindProperty("oneShot");
        playEvent = serializedObject.FindProperty("playEvent");
        stopEvent = serializedObject.FindProperty("stopEvent");
        radius = serializedObject.FindProperty("radius");
        gizmoColor = serializedObject.FindProperty("gizmoColor");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(mode);

        if ((FmodEmitterCustom.EmitterMode)mode.enumValueIndex == FmodEmitterCustom.EmitterMode.None)
        {
            serializedObject.ApplyModifiedProperties();
            return;
        }

        EditorGUILayout.Space();

        if ((FmodEmitterCustom.EmitterMode)mode.enumValueIndex == FmodEmitterCustom.EmitterMode.Basic)
        {
            DrawBasic();
        }
        else if ((FmodEmitterCustom.EmitterMode)mode.enumValueIndex == FmodEmitterCustom.EmitterMode.Advanced)
        {
            DrawBasic();
            EditorGUILayout.Space();
            DrawAdvanced();
        }

        EditorGUILayout.Space();
        DrawTriggers();

        EditorGUILayout.Space();

        serializedObject.ApplyModifiedProperties();
    }

    void DrawBasic()
    {
        EditorGUILayout.LabelField("Basic Config", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(eventId);
        EditorGUILayout.PropertyField(is3D);
        EditorGUILayout.PropertyField(oneShot);
    }

    void DrawAdvanced()
    {
        EditorGUILayout.LabelField("Advanced Settings", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(radius);
        EditorGUILayout.PropertyField(gizmoColor);
    }

    void DrawTriggers()
    {
        EditorGUILayout.LabelField("Triggers", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(playEvent);
        EditorGUILayout.PropertyField(stopEvent);
    }
}
#endif
