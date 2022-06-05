using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.Scripting;
using UnityEditor.SearchService;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityObject = UnityEngine.Object;
using RefId = System.Int64;

namespace Needle.Tiny
{
	internal static class MissingComponentHelper
	{
		[InitializeOnLoadMethod]
		private static async void Init()
		{
			EditorApplication.hierarchyChanged += UpdateInspector;
			Selection.selectionChanged += OnSelectionChanged;
			await Task.Delay(100);
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

				CollectMembersInfo("", prop.stringValue, serializedObject, out var members);

				var container = new IMGUIContainer();
				element.Add(container);
				container.onGUIHandler += OnGUI;

				void OnGUI()
				{
					try
					{
						const int offsetLeft = 16;
						// if (!prop.isValid) return;
						using (new GUILayout.HorizontalScope())
						{
							GUILayout.Space(offsetLeft);
							EditorGUILayout.LabelField(
								EditorGUIUtility.TrTextContentWithIcon("<color=#dddd44><b>Missing Type</b></color>: " + prop.stringValue, MessageType.Info),
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

		private class MemberInfo
		{
			public string Name;
			public string Value;
			public SerializedProperty Property;
		}

		private static void CollectMembersInfo(string id, string identifier, SerializedObject obj, out List<MemberInfo> members)
		{
			members = null;
			string[] lines = null;
			if (PrefabStageUtility.GetCurrentPrefabStage())
			{
				// TODO: render in prefab stage
			}
			else
			{
				var scene = SceneManager.GetActiveScene();
				lines = File.ReadAllLines(scene.path);
			}

			if (lines != null)
			{
				// TODO: this is the naive version, a component could be multiple times on an object and we also need to check for the gameobject id first and find the correct component etc
				var foundStart = false;
				foreach (var line in lines)
				{
					if (foundStart && line.StartsWith("---")) break;
					if (foundStart)
					{
						members ??= new List<MemberInfo>();
						var member = new MemberInfo();
						members.Add(member);
						var values = line.Split(':');
						member.Name = values[0].Trim();
						member.Value = values[1].Trim();
						member.Property = obj.FindProperty(member.Name);
					}
					else if (line.Contains(identifier))
					{
						foundStart = true;
					}
				}
			}
		}
	}
}