using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable CheckNamespace

namespace Needle.ComponentExtension
{
	internal static class MissingComponentHelper
	{
		[InitializeOnLoadMethod]
		private static async void Init()
		{
			EditorApplication.hierarchyChanged += UpdateInspector;
			Selection.selectionChanged += UpdateInspector;
			EditorApplication.focusChanged += OnFocusChanged;
			EditorApplication.RequestRepaintAllViews();
			do await Task.Delay(1000);
			while (EditorApplication.isCompiling || EditorApplication.isUpdating);
			UpdateInspector();
		}

		private static void OnFocusChanged(bool obj)
		{
			if (obj)
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
		private static Texture icon;

		private static void OnInject(Editor editor, EditorElement element)
		{
			// capture script type and store it in the serialized property
			var serializedObject = editor.serializedObject;
			var prop = serializedObject.FindProperty("m_EditorClassIdentifier");
			if (editor.target)
			{
				if (prop != null)
				{
					var identifier = editor.target.GetType().AssemblyQualifiedName;
					if (identifier != null && !prop.stringValue.StartsWith(identifier))
					{
						identifier = string.Join(",", identifier.Split(',').Take(2));
						prop.stringValue = identifier;
						serializedObject.ApplyModifiedProperties();
						EditorUtility.SetDirty(serializedObject.targetObject);
					}
				}
				return;
			}

			// render missing script info
			if (prop != null)
			{
				if (string.IsNullOrEmpty(prop.stringValue)) return;
				if (style == null)
				{
					style = new GUIStyle(EditorStyles.helpBox);
					style.richText = true;
				}

				Utils.CollectMembersInfo("", prop.stringValue, serializedObject, out var members);
				if (!icon) icon = AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath("06824066cef43c446a81e7fc2ef35664"));
				var message = "<color=#ffcc11><b>Missing Type</b></color>: " + prop.stringValue;
				var container = new IMGUIContainer();
				element.Add(container);
				container.onGUIHandler += OnGUI;

				void OnGUI()
				{
					try
					{
						const int offsetLeft = 16;
						// if (!prop.isValid) return; 
						GUILayout.Space(-5);
						using (new GUILayout.HorizontalScope())
						{
							GUILayout.Space(offsetLeft);
							EditorGUILayout.LabelField(
								EditorGUIUtility.TrTextContentWithIcon(message, icon),
								style);
							GUILayout.Space(3);
						}
						if (members != null)
						{
							using (new GUILayout.HorizontalScope())
							{
								using (new EditorGUI.DisabledScope(true))
								{
									GUILayout.Space(offsetLeft);
									foreach (var member in members)
									{
										if (member.Property != null)
											EditorGUILayout.PropertyField(member.Property, true);
										else EditorGUILayout.LabelField(member.Name, member.Value);
									}
								}
							}
						}
						GUILayout.Space(5);
					}
					catch (Exception)
					{
						container.onGUIHandler -= OnGUI;
					}
				}
			}
		}
	}
}