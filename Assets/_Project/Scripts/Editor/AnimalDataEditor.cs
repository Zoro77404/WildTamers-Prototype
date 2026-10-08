using UnityEditor;
using UnityEngine;
using WildTamers.Animals;

namespace WildTamers.EditorTools
{
    /// <summary>AnimalData Inspector: the normal fields, with the Special section open, plus a "Preview Special" button.</summary>
    [CustomEditor(typeof(AnimalData))]
    public class AnimalDataEditor : Editor
    {
        private bool expandedOnce;

        public override void OnInspectorGUI()
        {
            if (!expandedOnce)
            {
                var special = serializedObject.FindProperty("special");
                if (special != null) special.isExpanded = true;
                expandedOnce = true;
            }
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Preview Special", "Plays this animal's special-attack effect and sounds in the open scene, timed like in battle."),
                        GUILayout.Height(30f)))
                    SpecialPreview.Play((AnimalData)target);
                using (new EditorGUI.DisabledScope(!SpecialPreview.IsPlaying))
                {
                    if (GUILayout.Button("Stop", GUILayout.Width(60f), GUILayout.Height(30f))) SpecialPreview.Stop();
                }
            }
            EditorGUILayout.HelpBox("Open the Battle scene to preview on the real battle stages (it faces the battle camera); " +
                                    "in any other scene it plays in front of the Scene view camera. Change a value and press Preview again.",
                MessageType.None);
        }

        private void OnDisable()
        {
            // Leaving the Inspector mid-preview cleans up the effect and stand-in models.
            if (SpecialPreview.IsPlaying) SpecialPreview.Stop();
        }
    }
}
