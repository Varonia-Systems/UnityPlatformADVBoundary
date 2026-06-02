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
                    var so           = GetSerializedSettings();
                    var layerProp    = so.FindProperty("boundaryLayer");
                    var colliderProp = so.FindProperty("generateWallCollider");

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

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Collision", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(
                        "Si activé, un MeshCollider est généré sur le rideau (mur) de chaque " +
                        "segment de boundary au runtime, pour empêcher physiquement de traverser. " +
                        "Le collider reste actif même quand le mur n'est pas visible.",
                        MessageType.Info);

                    EditorGUI.BeginChangeCheck();
                    bool newGen = EditorGUILayout.Toggle(
                        new GUIContent("Generate Wall Collider"), colliderProp.boolValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        colliderProp.boolValue = newGen;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }

                    using (new EditorGUI.DisabledScope(!colliderProp.boolValue))
                    {
                        var thickProp = so.FindProperty("wallColliderThickness");
                        EditorGUI.BeginChangeCheck();
                        float newThick = EditorGUILayout.FloatField(
                            new GUIContent("Wall Collider Thickness (m)"), thickProp.floatValue);
                        if (EditorGUI.EndChangeCheck())
                        {
                            thickProp.floatValue = Mathf.Max(0.001f, newThick);
                            so.ApplyModifiedPropertiesWithoutUndo();
                        }

                        var triggerProp = so.FindProperty("wallColliderIsTrigger");
                        EditorGUI.BeginChangeCheck();
                        bool newTrigger = EditorGUILayout.Toggle(
                            new GUIContent("Is Trigger",
                                "Coché : trigger (détecte sans bloquer). Décoché : collider solide qui bloque."),
                            triggerProp.boolValue);
                        if (EditorGUI.EndChangeCheck())
                        {
                            triggerProp.boolValue = newTrigger;
                            so.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                },
                keywords = new HashSet<string>(new[] { "Varonia", "Boundary", "Advanced", "Layer", "Collider", "Collision", "Wall" })
            };
            return provider;
        }
    }
}
