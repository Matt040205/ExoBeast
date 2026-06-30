#if FMOD_PRESENT
using UnityEditor;

[CustomEditor(typeof(CreateFmodList))]
public class CreateFmodListEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty typeProp = serializedObject.FindProperty("type");
        SerializedProperty typeNameProp = serializedObject.FindProperty("typeName");
        SerializedProperty eventsProp = serializedObject.FindProperty("events");

        EditorGUILayout.PropertyField(typeProp);

        ListType currentType = (ListType)typeProp.enumValueIndex;

        if (currentType == ListType.Other)
        {
            EditorGUILayout.PropertyField(typeNameProp);
        }

        if (currentType != ListType.None)
        {
            EditorGUILayout.PropertyField(eventsProp, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

}
#endif
