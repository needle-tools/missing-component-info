using System;
using System.Collections.Generic;
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
			do await Task.Delay(1000);
			while (EditorApplication.isCompiling || EditorApplication.isUpdating);
			UpdateInspector();
		}

		// private static void OnFocusChanged(bool obj)
		// {
		// 	if (obj)
		// 		UpdateInspector();
		// }


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
						var id = GlobalObjectId.GetGlobalObjectIdSlow(editor.target);
						prop.stringValue = $"{identifier} $ " + id;
						serializedObject.ApplyModifiedProperties();
						EditorUtility.SetDirty(editor.target);
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

				if (!icon) icon = AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath("06824066cef43c446a81e7fc2ef35664"));
				var values = prop.stringValue.Split('$');
				var message = "<color=#ffcc11><b>Missing Type</b></color>: " + values[0];
				var container = new IMGUIContainer();
				element.Add(container);

				var serializedId = values.Length > 1 ? values[1] : string.Empty;


				var showMembers = false;
				var triedCollectingMembers = false;
				List<MemberInfo> members = default;
				const int offsetLeft = 16;

				container.onGUIHandler += OnGUI;

				void OnGUI()
				{
					try
					{
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

						if (Utils.CanShowProperties(editor.target)) 
							RenderSerializedProperties();
						GUILayout.Space(5);
					}
					catch (Exception ex)
					{
						Debug.LogException(ex);
						container.onGUIHandler -= OnGUI;
					}
				}

				void RenderSerializedProperties()
				{
					using (new GUILayout.HorizontalScope())
					{
						GUILayout.Space(offsetLeft);

						if (triedCollectingMembers && (members == null || members.Count <= 0))
						{
							using (new EditorGUI.DisabledScope(true))
								EditorGUILayout.LabelField("No serialized properties found");
						}
						else
							showMembers = EditorGUILayout.Foldout(showMembers, "Serialized Properties");
					}

					if (showMembers)
					{
						if (!triedCollectingMembers)
						{
							triedCollectingMembers = true;
							Utils.CollectMembersInfo(editor.target, serializedId, serializedObject, out members);
						}
						;
						if (members != null && members.Count > 0)
						{
							EditorGUI.indentLevel += 1;
							using (new EditorGUI.DisabledScope(true))
							{
								foreach (var member in members)
								{
									using (new GUILayout.HorizontalScope())
									{
										GUILayout.Space(offsetLeft);
										try
										{
											if (member.Property != null)
												EditorGUILayout.PropertyField(member.Property, true);
											else EditorGUILayout.LabelField(member.Name, member.Value);
										}
										catch (NullReferenceException)
										{
											// ignore
											EditorGUILayout.LabelField(member.Name, "Could not display");
										}
									}
								}
							}
							EditorGUI.indentLevel -= 1;
						}
					}
				}
			}
		}
	}
}