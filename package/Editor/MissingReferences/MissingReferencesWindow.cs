using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Needle.MissingReferences
{
    /// <summary>
    /// Scans the project for serialized references to missing (deleted) assets and other types of missing references and displays the results in an EditorWindow
    /// </summary>
    abstract class MissingReferencesWindow : EditorWindow
    {
        public const string MenuItemRootAnalysis = "Window/Analysis/";
        
        internal static class Styles
        {
            const string k_IncludeEmptyEventsLabel = "Include Empty Events";
            const string k_IncludeEmptyEventsTooltip = "While scanning properties for missing references, include serialized UnityEvent references which do not have a target object. Missing references to target objects will be included whether or not this is set.";
            const string k_IncludeMissingMethodsLabel = "Include Missing Methods";
            const string k_IncludeMissingMethodsTooltip = "While scanning properties for missing references, include serialized UnityEvent references which specify methods that do not exist";
            const string k_IncludeUnsetMethodsLabel = "Include Unset Methods";
            const string k_IncludeUnsetMethodsTooltip = "While scanning properties for missing references, include serialized UnityEvent references which do not specify a method";

            public static readonly GUIStyle RichTextFoldout;
            public static readonly GUIStyle RichTextLabel;
            public static readonly GUIContent IncludeEmptyEventsContent;
            public static readonly GUIContent IncludeMissingMethodsContent;
            public static readonly GUIContent IncludeUnsetMethodsContent;

            static Styles()
            {
                RichTextFoldout = EditorStyles.foldout;
                RichTextFoldout.richText = true;

                RichTextLabel = EditorStyles.label;
                RichTextLabel.richText = true;

                IncludeEmptyEventsContent = new GUIContent(k_IncludeEmptyEventsLabel, k_IncludeEmptyEventsTooltip);
                IncludeMissingMethodsContent = new GUIContent(k_IncludeMissingMethodsLabel, k_IncludeMissingMethodsTooltip);
                IncludeUnsetMethodsContent = new GUIContent(k_IncludeUnsetMethodsLabel, k_IncludeUnsetMethodsTooltip);
            }
        }

        const float k_LabelWidthRatio = 0.5f;
        const string k_ScanButtonName = "Scan";
        const string k_MissingMethodFormat = "Missing Method: {0}";

        SceneScanner.Options m_Options = new SceneScanner.Options();

        /// <summary>
        /// Scan for missing serialized references
        /// </summary>
        /// <param name="options">User-configurable options for this view</param>
        protected abstract void Scan(SceneScanner.Options options);

        protected virtual void OnGUI()
        {
            EditorGUIUtility.labelWidth = position.width * k_LabelWidthRatio;
            m_Options.IncludeEmptyEvents = EditorGUILayout.Toggle(Styles.IncludeEmptyEventsContent, m_Options.IncludeEmptyEvents);
            m_Options.IncludeMissingMethods = EditorGUILayout.Toggle(Styles.IncludeMissingMethodsContent, m_Options.IncludeMissingMethods);
            m_Options.IncludeUnsetMethods = EditorGUILayout.Toggle(Styles.IncludeUnsetMethodsContent, m_Options.IncludeUnsetMethods);
            if (GUILayout.Button(k_ScanButtonName))
                Scan(m_Options);
        }

        protected virtual void DrawItem(Rect selectionRect)
        {
            selectionRect.xMin = selectionRect.xMax - 2;
            selectionRect.x -= 4;
            var c = GUI.color;
            GUI.color = Color.red;
            GUI.DrawTexture(selectionRect, Texture2D.whiteTexture);
            GUI.color = c;
        }

        /// <summary>
        /// Draw the missing references UI for a list of properties known to have missing references
        /// </summary>
        /// <param name="properties">A list of SerializedProperty objects known to have missing references</param>
        internal static void DrawPropertiesWithMissingReferences(List<SerializedProperty> properties)
        {
            foreach (var property in properties)
            {
                switch (property.propertyType)
                {
                    // The only way a generic property could be in the list of missing properties is if it
                    // is a serialized UnityEvent with its method missing
                    case SerializedPropertyType.Generic:
                        // cases we don't want to show as missing: 
                        EditorGUILayout.LabelField(string.Format(k_MissingMethodFormat, property.propertyPath));
                        break;
                    case SerializedPropertyType.ObjectReference:
                        EditorGUILayout.PropertyField(property, new GUIContent(property.propertyPath));
                        break;
                }
            }
        }
    }
}
