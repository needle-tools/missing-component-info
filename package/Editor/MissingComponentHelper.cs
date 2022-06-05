using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SearchService;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Needle.Tiny
{
	internal static class MissingComponentHelper
	{
		[InitializeOnLoadMethod]
		private static void Init()
		{
			EditorApplication.hierarchyChanged += UpdateInspector;
			Selection.selectionChanged += OnSelectionChanged;
			UpdateInspector();
		}

		private static void OnSelectionChanged()
		{
			UpdateInspector();
		}

		private const string InjectionClassName = "__needle_missingcomponent_helper";

		private static void UpdateInspector()
		{
			var openEditors = ActiveEditorTracker.sharedTracker.activeEditors;
			foreach (var ed in openEditors)
			{
				var inspectors = InspectorWindow.GetInspectors();
				foreach (var ins in inspectors)
				{
					ins.rootVisualElement.Query<EditorElement>().ForEach(editorElement =>
					{
						if (editorElement.editor != ed) return;
						if (editorElement.ClassListContains(InjectionClassName)) return;
						editorElement.AddToClassList(InjectionClassName);
						try
						{
							OnInject(ed, editorElement);
						}
						catch (Exception e)
						{
							Debug.LogException(e);
						}
					});
				}
			}
		}

		private static GUIStyle style;

		private static void OnInject(Editor editor, EditorElement element)
		{
			var serializedObject = editor.serializedObject;
			var prop = serializedObject.FindProperty("m_EditorClassIdentifier");
			if (editor.target)
			{
				if (prop != null)
				{
					AssetDatabase.TryGetGUIDAndLocalFileIdentifier(editor.target, out var guid, out long id);
					var identifier = editor.target.GetType().AssemblyQualifiedName + " / " + guid + "@" + id;
					if (identifier != prop.stringValue)
					{
						prop.stringValue = editor.target.GetType().AssemblyQualifiedName;
						serializedObject.ApplyModifiedProperties();
					}
				}
				return;
			}
			
			if(prop != null)
			{
				if (string.IsNullOrEmpty(prop.stringValue)) return;
				if (style == null)
				{
					style = new GUIStyle(EditorStyles.helpBox);
					style.richText = true; 
				}
				
				var container = new IMGUIContainer(); 
				element.Add(container);
				container.onGUIHandler += () =>
				{
					if (!prop.isValid) return;
					using (new GUILayout.HorizontalScope())
					{
						GUILayout.Space(16);
						EditorGUILayout.LabelField(EditorGUIUtility.TrTextContentWithIcon("<color=#dddd44><b>Missing Type</b></color>: " + prop.stringValue, MessageType.Info), style);
						GUILayout.Space(3);
					}
					GUILayout.Space(5);
				};
			}
		}
	}
}