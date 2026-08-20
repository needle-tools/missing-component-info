using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
#if UNITY_6000_4_OR_NEWER
using ObjectId = UnityEngine.EntityId;
#else
using ObjectId = System.Int32;
#endif

namespace Needle.MissingReferences
{
    /// <summary>
    /// Scans all loaded scenes for references to missing (deleted) assets and other types of missing references and displays the results in an EditorWindow
    /// </summary>
    sealed class MissingSceneReferences : MissingReferencesWindow
    {
        const string k_Instructions = "Click the Scan button to scan the active scene for missing references. " +
            "WARNING: For large scenes, this may take a long time and/or crash the Editor.";

        const string k_NoMissingReferences = "No missing references in active scene";

        // Bool fields will be serialized to maintain state between domain reloads, but our list of GameObjects will not
        [NonSerialized]
        bool m_Scanned;

        Vector2 m_ScrollPosition;
        // readonly List<KeyValuePair<string, GameObjectContainer>> m_SceneRoots = new List<KeyValuePair<string, GameObjectContainer>>();

        [MenuItem(MenuItemRootAnalysis + "Missing Scene References", priority = -898)]
        static void OnMenuItem() { GetWindow<MissingSceneReferences>("Missing Scene References"); }

        private SceneScanner scanner = null;
        
        /// <summary>
        /// Scan all assets in the active scene for missing serialized references
        /// </summary>
        /// <param name="options">User-configurable options for this view</param>
        protected override void Scan(SceneScanner.Options options)
        {
            m_Scanned = true;

            scanner = new SceneScanner(options);
            scanner.FindMissingReferences();

            var missing = scanner.MissingReferences;

            allMissingReferences = missing
                .Where(x => x.Object)
                .ToLookup(x => GetId(x.Object));

            foreach (var reference in missing)
            {
                EditorGUIUtility.PingObject(reference.Object);
            }
            
            EditorApplication.RepaintHierarchyWindow();
        }

        private ILookup<ObjectId, GameObjectContainer> allMissingReferences;

        private static ObjectId GetId(UnityEngine.Object obj)
        {
#if UNITY_6000_4_OR_NEWER
            return obj.GetEntityId();
#else
            return obj.GetInstanceID();
#endif
        }
        
        protected override void OnGUI()
        {
            base.OnGUI();

            if (!m_Scanned)
            {
                EditorGUILayout.HelpBox(k_Instructions, MessageType.Info);
                GUIUtility.ExitGUI();
            }

            if (scanner.SceneRoots.Count == 0)
            {
                GUILayout.Label(k_NoMissingReferences);
            }
            else
            {
                using (var scrollView = new GUILayout.ScrollViewScope(m_ScrollPosition))
                {
                    m_ScrollPosition = scrollView.scrollPosition;
                    foreach (var kvp in scanner.SceneRoots)
                    {
                        kvp.Value.Draw();
                    }
                }
            }
        }
        
        private void OnEnable()
        {
#if UNITY_6000_4_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += HierarchyWindowItemOnGUI;
#else
            EditorApplication.hierarchyWindowItemOnGUI += HierarchyWindowItemOnGUI;
#endif
            EditorApplication.RepaintHierarchyWindow();
        }

        private void OnDisable()
        {
#if UNITY_6000_4_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI -= HierarchyWindowItemOnGUI;
#else
            EditorApplication.hierarchyWindowItemOnGUI -= HierarchyWindowItemOnGUI;
#endif
            EditorApplication.RepaintHierarchyWindow();
        }

        private void HierarchyWindowItemOnGUI(ObjectId entityId, Rect selectionRect)
        {
            if (allMissingReferences == null) return;
            if (!allMissingReferences.Contains(entityId)) return;

            DrawItem(selectionRect);
        }
    }
}
