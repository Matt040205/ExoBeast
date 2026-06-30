#if FMOD_PRESENT
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(FmodSlider.BusSlider))]
public class FmodSliderEditor : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty nameProp = property.FindPropertyRelative("name");

        if (!string.IsNullOrEmpty(nameProp.stringValue))
        {
            label.text = nameProp.stringValue;
        }

        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, true);
    }
}
#endif
