using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VaroniaBackOffice.EditorTools
{
    /// <summary>
    /// Onglet Project Settings ▸ Varonia ▸ Advanced Boundary.
    /// Permet de choisir le layer appliqué à tous les objets de la boundary
    /// lors de leur instanciation au runtime.
    /// </summary>
    static class AdvBoundarySettingsProvider
    {
        const string ResourcesDir = "Packages/com.varonia.advspatial/Runtime/Resources";
        const string AssetPath    = ResourcesDir + "/" + AdvBoundarySettings.ResourceName + ".asset";

        internal static AdvBoundarySettings GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<AdvBoundarySettings>(AssetPath);
            if (settings == null)
                settings = Resources.Load<AdvBoundarySettings>(AdvBoundarySettings.ResourceName);

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<AdvBoundarySettings>();
                Directory.CreateDirectory(ResourcesDir);
                AssetDatabase.CreateAsset(settings, AssetPath);
                AssetDatabase.SaveAssets();
            }
            return settings;
        }

        static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            var provider = new SettingsProvider("Project/Varonia/Advanced Boundary", SettingsScope.Project)
            {
                label = "Advanced Boundary",
                guiHandler = _ =>
                {
                    var so        = GetSerializedSettings();
                    var layerProp = so.FindProperty("boundaryLayer");

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Instanciation", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(
                        "Layer appliqué à tous les GameObjects générés par la boundary " +
                        "(contours au sol + segments de mur) lors de leur création au runtime.",
                        MessageType.Info);

                    EditorGUI.BeginChangeCheck();
                    int newLayer = EditorGUILayout.LayerField(
                        new GUIContent("Boundary Layer"), layerProp.intValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        layerProp.intValue = newLayer;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                },
                keywords = new HashSet<string>(new[] { "Varonia", "Boundary", "Advanced", "Layer" })
            };
            return provider;
        }
    }
}
