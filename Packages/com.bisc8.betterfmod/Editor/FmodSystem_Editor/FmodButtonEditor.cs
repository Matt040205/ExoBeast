#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(FmodButtonAction))]
public class FmodButtonEditor : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty moment = property.FindPropertyRelative("moment");
        SerializedProperty command = property.FindPropertyRelative("command");
        SerializedProperty soundId = property.FindPropertyRelative("soundId");
        SerializedProperty fade = property.FindPropertyRelative("fade");

        string elementName = moment.enumDisplayNames[moment.enumValueIndex];

        EditorGUI.BeginProperty(position, new GUIContent(elementName), property);

        Rect rect = new Rect(
            position.x,
            position.y,
            position.width,
            EditorGUIUtility.singleLineHeight
        );

        property.isExpanded = EditorGUI.Foldout(
            rect,
            property.isExpanded,
            elementName,
            true
        );

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            rect.y += EditorGUIUtility.singleLineHeight + 2f;
            EditorGUI.PropertyField(rect, moment);

            if (moment.enumValueIndex != 0)
            {
                rect.y += EditorGUIUtility.singleLineHeight + 2f;
                EditorGUI.PropertyField(rect, command);

                rect.y += EditorGUIUtility.singleLineHeight + 2f;
                EditorGUI.PropertyField(rect, soundId);

                if (command.enumValueIndex == (int)FmodCommandType.Stop)
                {
                    rect.y += EditorGUIUtility.singleLineHeight + 2f;
                    EditorGUI.PropertyField(rect, fade);
                }
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        SerializedProperty moment = property.FindPropertyRelative("moment");
        SerializedProperty command = property.FindPropertyRelative("command");

        int lines = 2;

        if (moment.enumValueIndex != 0)
        {
            lines += 2;

            if (command.enumValueIndex == (int)FmodCommandType.Stop)
                lines += 1;
        }

        return lines * (EditorGUIUtility.singleLineHeight + 2f);
    }
}
#endif