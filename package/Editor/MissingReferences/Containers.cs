using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Needle.MissingReferences
{
    /// <summary>
    /// Base class for asset and prefab references, so they can exist in the same list
    /// </summary>
    public abstract class MissingReferencesContainer
    {
        public abstract void Draw();
        public abstract UnityObject Object { get; }
        public abstract void SetVisibleRecursively(bool visible);
    }
    
    /// <summary>
    /// Tree structure for GameObject scan results
    /// When the Scan method encounters a GameObject in a scene or a prefab in the project, we initialize one of
    /// these using the GameObject as an argument. This scans the object and its components/children, retaining
    /// the results for display in the GUI. The window calls into these helper objects to draw them, as well.
    /// </summary>
    public class GameObjectContainer : MissingReferencesContainer
    {
        /// <summary>
        /// Container for component scan results. Just as with GameObjectContainer, we initialize one of these
        /// using a component to scan it for missing references and retain the results
        /// </summary>
        internal class ComponentContainer
        {
            const string k_MissingScriptLabel = "<color=red>Missing Script!</color>";

            readonly Component m_Component;
            readonly string m_NameOverride;
            public readonly List<SerializedProperty> PropertiesWithMissingReferences = new List<SerializedProperty>();

            /// <summary>
            /// Initialize a ComponentContainer to represent the given Component
            /// This will scan the component for missing references and retain the information for display in
            /// the given window.
            /// </summary>
            /// <param name="component">The Component to scan for missing references</param>
            /// <param name="options">User-configurable options for this view</param>
            public ComponentContainer(Component component, SceneScanner.Options options)
            {
                m_Component = component;
                SceneScanner.CheckForMissingReferences(component, PropertiesWithMissingReferences, options);
            }

            public ComponentContainer(SerializedObject serializedObject, SceneScanner.Options options)
            {
                if (serializedObject == null || !serializedObject.targetObject) return;
                
                m_NameOverride = serializedObject.targetObject.name;
                SceneScanner.CheckForMissingReferences(serializedObject, PropertiesWithMissingReferences, options);
            }

            /// <summary>
            /// Draw the missing references UI for this component
            /// </summary>
            public void Draw()
            {
                if (m_NameOverride != null)
                    EditorGUILayout.LabelField(m_NameOverride);
                else
                    EditorGUILayout.ObjectField(m_Component, typeof(Component), false);
                
                using (new EditorGUI.IndentLevelScope())
                {
                    // If the component equates to null, it is an empty scripting wrapper, indicating a missing script
                    if (m_Component == null && m_NameOverride == null)
                    {
                        EditorGUILayout.LabelField(k_MissingScriptLabel, MissingReferencesWindow.Styles.RichTextLabel);
                        return;
                    }

                    MissingReferencesWindow.DrawPropertiesWithMissingReferences(PropertiesWithMissingReferences);
                }
            }

            private static PropertyInfo objectReferenceTypeString;
            public void FormatAsLog(StringBuilder target, int indentation)
            {
                var indentationString = new string(' ', indentation * 4) + "- ";;
                if (m_NameOverride != null)
                    target.AppendLine(indentationString + m_NameOverride + ":");
                else if (m_Component == null)
                    target.AppendLine(indentationString + "Missing script:");
                
                if (objectReferenceTypeString == null) objectReferenceTypeString = typeof(SerializedProperty).GetProperty(nameof(objectReferenceTypeString), (BindingFlags)(-1));
                foreach (var property in PropertiesWithMissingReferences)
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        // TODO can we show the GUID of a missing asset at least?
                        // AssetDatabase.TryGetGUIDAndLocalFileIdentifier(property.objectReferenceValue, out var guid, out long _);
                        target.AppendLine("    " + indentationString + $"{property.propertyPath} ({objectReferenceTypeString?.GetValue(property)})");
                    }
                    else if (property.propertyType == SerializedPropertyType.ManagedReference)
                        target.AppendLine("    " + indentationString + $"{property.propertyPath} ({property.managedReferenceFullTypename})");
                    else
                        target.AppendLine("    " + indentationString + $"{property.propertyPath} ({property.type})");
                }
            }
        }

        const int k_PingButtonWidth = 35;
        const string k_PingButtonLabel = "Ping";
        const string k_MissingPrefabLabelFormat = "<color=red>{0} - Missing Prefab</color>";
        const string k_LabelFormat = "{0}: {1}";
        const string k_ComponentsGroupLabelFormat = "Components: {0}";
        const string k_ChildrenGroupLabelFormat = "Children: {0}";
        
        readonly GameObject m_GameObject;
        readonly List<GameObjectContainer> m_Children = new List<GameObjectContainer>();
        readonly List<ComponentContainer> m_Components = new List<ComponentContainer>();
        internal List<GameObjectContainer> Children => m_Children;
        internal List<ComponentContainer> Components => m_Components;
        
        bool m_IsMissingPrefab;
        int m_MissingReferencesInChildren;
        int m_MissingReferencesInComponents;

        internal bool HasMissingReferences => m_IsMissingPrefab || m_MissingReferencesInComponents > 0;
        
        bool m_Visible;
        bool m_ShowComponents;
        bool m_ShowChildren;

        public int Count { get; private set; }
        public override UnityObject Object { get { return m_GameObject; } }

        public GameObjectContainer() { }

        /// <summary>
        /// Initialize a GameObjectContainer to represent the given GameObject
        /// This will scan the component for missing references and retain the information for display in
        /// the given window.
        /// </summary>
        /// <param name="gameObject">The GameObject to scan for missing references</param>
        /// <param name="options">User-configurable options for this view</param>
        internal GameObjectContainer(GameObject gameObject, SceneScanner.Options options)
        {
            m_GameObject = gameObject;

            if (PrefabUtility.IsAnyPrefabInstanceRoot(gameObject))
                m_IsMissingPrefab = PrefabUtility.IsPrefabAssetMissing(gameObject);

            foreach (var component in gameObject.GetComponents<Component>())
            {
                var container = new ComponentContainer(component, options);
                if (component == null)
                {
                    m_Components.Add(container);
                    Count++;
                    m_MissingReferencesInComponents++;
                    continue;
                }

                var count = container.PropertiesWithMissingReferences.Count;
                if (count > 0)
                {
                    m_Components.Add(container);
                    Count += count;
                    m_MissingReferencesInComponents += count;
                }
            }

            foreach (Transform child in gameObject.transform)
            {
                AddChild(child.gameObject, options);
            }
        }

        /// <summary>
        /// Add a child GameObject to this GameObjectContainer
        /// </summary>
        /// <param name="gameObject">The GameObject to scan for missing references</param>
        /// <param name="options">User-configurable options for this view</param>
        public void AddChild(GameObject gameObject, SceneScanner.Options options)
        {
            var child = new GameObjectContainer(gameObject, options);
            var childCount = child.Count;
            Count += childCount;
            m_MissingReferencesInChildren += childCount;

            var isMissingPrefab = child.m_IsMissingPrefab;
            if (isMissingPrefab)
            {
                m_MissingReferencesInChildren++;
                Count++;
            }

            if (childCount > 0 || isMissingPrefab)
                m_Children.Add(child);
        }

        public void CheckRenderSettings(SceneScanner.Options options)
        {
            var GetRenderSettings = typeof(RenderSettings).GetMethod("GetRenderSettings", (BindingFlags)(-1));
            if (GetRenderSettings == null) return;
            
            var so = new SerializedObject(GetRenderSettings.Invoke(null, null) as RenderSettings);
            var container = new ComponentContainer(so, options);
            
            var count = container.PropertiesWithMissingReferences.Count;
            if (count > 0)
            {
                Count += count;
                m_MissingReferencesInComponents += count;
                m_Components.Add(container);
            }
        }

        /// <summary>
        /// Draw missing reference information for this GameObjectContainer
        /// </summary>
        public override void Draw()
        {
            var wasVisible = m_Visible;

            var firstGo = m_Children?.FirstOrDefault()?.m_GameObject;
            var label = string.Format(k_LabelFormat, m_GameObject ? m_GameObject.name : (firstGo ? firstGo.scene.name : "Scene Root"), Count);
            if (m_IsMissingPrefab)
                label = string.Format(k_MissingPrefabLabelFormat, label);

            // If this object has 0 missing references but is being drawn, it is a missing prefab with no overrides
            if (Count == 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(label, MissingReferencesWindow.Styles.RichTextLabel);
                    if (GUILayout.Button(k_PingButtonLabel, GUILayout.Width(k_PingButtonWidth)))
                        EditorGUIUtility.PingObject(m_GameObject);
                }

                return;
            }

            m_Visible = EditorGUILayout.Foldout(m_Visible, label, true, MissingReferencesWindow.Styles.RichTextFoldout);

            // Hold alt to apply visibility state to all children (recursively)
            if (m_Visible != wasVisible && Event.current.alt)
                SetVisibleRecursively(m_Visible);

            if (!m_Visible)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                if (m_MissingReferencesInComponents > 0)
                {
                    if (m_GameObject != null)
                        EditorGUILayout.ObjectField(m_GameObject, typeof(GameObject), true);
                    
                    label = string.Format(k_ComponentsGroupLabelFormat, m_MissingReferencesInComponents);
                    m_ShowComponents = EditorGUILayout.Foldout(m_ShowComponents, label, true);
                    if (m_ShowComponents)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            foreach (var component in m_Components)
                            {
                                component.Draw();
                            }
                        }
                    }
                }
                
                // If m_GameObject is null, this is a scene
                if (m_GameObject == null)
                {
                    DrawChildren();
                    return;
                }

                if (m_MissingReferencesInChildren > 0)
                {
                    label = string.Format(k_ChildrenGroupLabelFormat, m_MissingReferencesInChildren);
                    m_ShowChildren = EditorGUILayout.Foldout(m_ShowChildren, label, true);
                    if (m_ShowChildren)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            DrawChildren();
                        }
                    }
                }
            }

            void DrawChildren()
            {
                foreach (var child in m_Children)
                {
                    var childObject = child.m_GameObject;

                    // Check for null in case of destroyed object
                    if (childObject)
                        child.Draw();
                }
            }
        }

        /// <summary>
        /// Set the visibility state of this object and all of its children
        /// </summary>
        /// <param name="visible">Whether this object and its children should be visible in the GUI</param>
        public override void SetVisibleRecursively(bool visible)
        {
            m_Visible = visible;
            m_ShowComponents = visible;
            m_ShowChildren = visible;
            foreach (var child in m_Children)
            {
                child.SetVisibleRecursively(visible);
            }
        }

        public void FormatAsLog(StringBuilder target)
        {
            Log(target, 0);
        }

        private void Log(StringBuilder target, int indentation)
        {
            var indentationString = new string(' ', indentation * 4) + (Object ? "- " : "  ");
            var displayName = Object ? Object.name : "Scene";
            if (!Object)
            {
                var firstChild = Children?.FirstOrDefault()?.Object as GameObject;
                if (firstChild)
                    displayName = $"Scene: {firstChild.scene.name}";
            }
            target.AppendLine(indentationString + displayName);

            if (Components != null)
            {
                foreach (var component in Components)
                {
                    component.FormatAsLog(target, indentation);
                }
            }

            if (Children != null)
            {
                foreach (var children in Children)
                {
                    children.Log(target, indentation + 1);
                }
            }
        }
    }
}