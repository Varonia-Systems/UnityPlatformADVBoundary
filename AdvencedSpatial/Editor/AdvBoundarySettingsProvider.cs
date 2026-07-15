using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VaroniaBackOffice.EditorTools
{
    /// <summary>
    /// Onglet Project Settings ▸ Varonia ▸ Advanced Boundary.
    /// Skin cartes/accent aligné sur "Varonia Back Office". Gère le layer, les colliders de mur,
    /// les prefabs d'obstacles par défaut (Small/Medium/Large) et des overrides par scène.
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

        // ─── Palette (alignée sur Varonia Back Office) ───────────────────────────────

        static readonly Color ColHeader = new Color(0.15f, 0.15f, 0.18f, 1f);
        static readonly Color AccentBlue   = new Color(0.25f, 0.55f, 1.00f, 1f);
        static readonly Color AccentGreen  = new Color(0.20f, 0.80f, 0.45f, 1f);
        static readonly Color AccentOrange = new Color(1.00f, 0.60f, 0.10f, 1f);
        static readonly Color AccentPurple = new Color(0.65f, 0.35f, 1.00f, 1f);
        static readonly Color ColSeparator = new Color(0.35f, 0.35f, 0.40f, 1f);

        static GUIStyle _card, _sectionTitle, _desc, _version;

        static void EnsureStyles()
        {
            if (_card != null) return;

            _card = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(12, 12, 10, 10),
                margin  = new RectOffset(0, 0, 4, 4),
                normal  = { background = MakeTex(new Color(0.18f, 0.18f, 0.22f, 1f)) }
            };
            _sectionTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
            _desc = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10, wordWrap = true,
                normal = { textColor = new Color(0.65f, 0.65f, 0.70f, 1f) }
            };
            _version = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 9 };
        }

        static Texture2D MakeTex(Color col)
        {
            var t = new Texture2D(1, 1); t.SetPixel(0, 0, col); t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave; return t;
        }

        // ─── SettingsProvider ───────────────────────────────────────────────────────

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/Varonia/Advanced Boundary", SettingsScope.Project)
            {
                label = "Advanced Boundary",
                keywords = new HashSet<string>(new[]
                    { "Varonia", "Boundary", "Advanced", "Layer", "Collider", "Collision", "Wall", "Obstacle", "Prefab", "Scene" }),
                guiHandler = _ => DrawGUI()
            };
        }

        static void DrawGUI()
        {
            EnsureStyles();

            var settings = GetOrCreateSettings();
            var so = new SerializedObject(settings);
            so.Update();

            DrawHeader();
            GUILayout.Space(6);

            // ── Instanciation ──
            DrawSectionCard("🧱  Instantiation",
                "Layer appliqué à tous les GameObjects générés par la boundary (contours au sol + " +
                "segments de mur) lors de leur création au runtime.",
                AccentBlue, () =>
                {
                    var layerProp = so.FindProperty("boundaryLayer");
                    layerProp.intValue = EditorGUILayout.LayerField(new GUIContent("Boundary Layer"), layerProp.intValue);
                });

            // ── Collision ──
            DrawSectionCard("🚧  Collision",
                "Génère un BoxCollider fin sur le rideau (mur) de chaque segment au runtime, pour " +
                "détecter ou empêcher le passage. Le collider reste actif même mur non visible.",
                AccentOrange, () =>
                {
                    var colliderProp = so.FindProperty("generateWallCollider");
                    EditorGUILayout.PropertyField(colliderProp, new GUIContent("Generate Wall Collider"));

                    using (new EditorGUI.DisabledScope(!colliderProp.boolValue))
                    {
                        var thickProp = so.FindProperty("wallColliderThickness");
                        thickProp.floatValue = Mathf.Max(0.001f, EditorGUILayout.FloatField(
                            new GUIContent("Wall Collider Thickness (m)"), thickProp.floatValue));

                        EditorGUILayout.PropertyField(so.FindProperty("wallColliderIsTrigger"),
                            new GUIContent("Is Trigger",
                                "Coché : trigger (détecte sans bloquer). Décoché : collider solide qui bloque."));
                    }
                });

            // ── Obstacle Prefabs (défaut) ──
            DrawSectionCard("📦  Obstacle Prefabs (default)",
                "Prefab instancié pour chaque obstacle du JSON selon sa taille. À l'instanciation, " +
                "Position / Rotation / Scale sont appliqués ; un composant IAdvObstacle reçoit en plus " +
                "les données complètes (dont SpecialId).",
                AccentPurple, () =>
                {
                    EditorGUILayout.PropertyField(so.FindProperty("obstaclePrefabSmall"),  new GUIContent("Small"));
                    EditorGUILayout.PropertyField(so.FindProperty("obstaclePrefabMedium"), new GUIContent("Medium"));
                    EditorGUILayout.PropertyField(so.FindProperty("obstaclePrefabLarge"),  new GUIContent("Large"));
                });

            // ── Overrides par scène ──
            DrawSectionCard("🗺  Scene Overrides",
                "Remplace les prefabs d'obstacles pour une scène précise. Ex. : scène futuriste → " +
                "obstacles futuristes ; grotte → cailloux. Un slot laissé vide retombe sur le prefab " +
                "par défaut de cette taille.",
                AccentGreen, () => DrawSceneOverrides(so));

            GUILayout.Space(10);
            DrawSeparator(ColSeparator);
            GUILayout.Space(4);
            EditorGUILayout.LabelField("Varonia  •  Advanced Boundary", _version);
            GUILayout.Space(4);

            if (so.ApplyModifiedProperties())
                EditorUtility.SetDirty(settings);
        }

        // ─── Scene Overrides ────────────────────────────────────────────────────────

        static void DrawSceneOverrides(SerializedObject so)
        {
            var listProp = so.FindProperty("sceneOverrides");
            string[] scenes = GetBuildSceneNames();

            if (listProp.arraySize == 0)
                GUILayout.Label("Aucun override. Toutes les scènes utilisent les prefabs par défaut.", _desc);

            for (int i = 0; i < listProp.arraySize; i++)
            {
                var el     = listProp.GetArrayElementAtIndex(i);
                var nameP  = el.FindPropertyRelative("sceneName");
                var smallP = el.FindPropertyRelative("small");
                var medP   = el.FindPropertyRelative("medium");
                var largeP = el.FindPropertyRelative("large");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Scène #{i}", EditorStyles.boldLabel, GUILayout.Width(64));
                DrawSceneDropdown(nameP, scenes);
                bool remove = GUILayout.Button("✕", GUILayout.Width(24));
                EditorGUILayout.EndHorizontal();

                if (remove)
                {
                    listProp.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndVertical();
                    break;
                }

                GUILayout.Space(3);
                EditorGUILayout.PropertyField(smallP, new GUIContent("Small"));
                EditorGUILayout.PropertyField(medP,   new GUIContent("Medium"));
                EditorGUILayout.PropertyField(largeP, new GUIContent("Large"));
                GUILayout.Label("Slot vide → prefab par défaut de cette taille.", _desc);

                EditorGUILayout.EndVertical();
                GUILayout.Space(4);
            }

            if (GUILayout.Button("+ Add scene override"))
            {
                listProp.InsertArrayElementAtIndex(listProp.arraySize);
                var el = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                el.FindPropertyRelative("sceneName").stringValue = scenes.Length > 0 ? scenes[0] : "";
                el.FindPropertyRelative("small").objectReferenceValue  = null;
                el.FindPropertyRelative("medium").objectReferenceValue = null;
                el.FindPropertyRelative("large").objectReferenceValue  = null;
            }
        }

        static void DrawSceneDropdown(SerializedProperty nameP, string[] scenes)
        {
            int idx = Array.IndexOf(scenes, nameP.stringValue);
            if (idx < 0 && !string.IsNullOrEmpty(nameP.stringValue))
            {
                // Valeur hors build : on la conserve en tête pour ne pas la perdre.
                var opts = new List<string> { nameP.stringValue + "  (hors build)" };
                opts.AddRange(scenes);
                int ni = EditorGUILayout.Popup(0, opts.ToArray());
                if (ni > 0) nameP.stringValue = scenes[ni - 1];
            }
            else if (scenes.Length > 0)
            {
                int ni = EditorGUILayout.Popup(Mathf.Max(0, idx), scenes);
                nameP.stringValue = scenes[Mathf.Clamp(ni, 0, scenes.Length - 1)];
            }
            else
            {
                nameP.stringValue = EditorGUILayout.TextField(nameP.stringValue);
            }
        }

        static string[] GetBuildSceneNames()
        {
            var list = new List<string>();
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (string.IsNullOrEmpty(s.path)) continue;
                list.Add(Path.GetFileNameWithoutExtension(s.path));
            }
            return list.ToArray();
        }

        // ─── Composants UI (skin) ────────────────────────────────────────────────────

        static void DrawHeader()
        {
            var rect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(rect, ColHeader);

            GUILayout.Space(10);
            var barRect = GUILayoutUtility.GetRect(0, 2, GUILayout.ExpandWidth(true));
            float third = barRect.width / 3f;
            EditorGUI.DrawRect(new Rect(barRect.x,             barRect.y, third, barRect.height), AccentBlue);
            EditorGUI.DrawRect(new Rect(barRect.x + third,     barRect.y, third, barRect.height), AccentGreen);
            EditorGUI.DrawRect(new Rect(barRect.x + third * 2, barRect.y, third, barRect.height), AccentPurple);
            GUILayout.Space(8);

            EditorGUILayout.LabelField("ADVANCED BOUNDARY", new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white }
            });
            EditorGUILayout.LabelField("Boundary • Obstacles • Scene Overrides",
                new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 10 });

            GUILayout.Space(8);
            EditorGUILayout.EndVertical();
        }

        static void DrawSectionCard(string title, string description, Color accent, Action content)
        {
            EditorGUILayout.BeginVertical(_card);

            EditorGUILayout.BeginHorizontal();
            var accentRect = GUILayoutUtility.GetRect(3, 18, GUILayout.Width(3));
            EditorGUI.DrawRect(accentRect, accent);
            GUILayout.Space(6);
            EditorGUILayout.LabelField(title, _sectionTitle);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(2);
            EditorGUILayout.LabelField(description, _desc);
            GUILayout.Space(6);
            DrawSeparator(new Color(0.30f, 0.30f, 0.35f, 1f));
            GUILayout.Space(6);

            content?.Invoke();

            EditorGUILayout.EndVertical();
        }

        static void DrawSeparator(Color color)
        {
            var rect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, color);
        }
    }
}
