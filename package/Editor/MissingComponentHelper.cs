using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

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
			var inspectors = InspectorWindow.GetInspectors();
			foreach (var ed in openEditors)
			{
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

		private static readonly Dictionary<string, List<ScriptCandidate>> candidatesPerType = new Dictionary<string, List<ScriptCandidate>>();

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
				var typeInfo = values[0];
				var message = "<color=#ffcc11><b>Missing Type</b></color>: " + typeInfo;
				var container = new IMGUIContainer();
				element.Add(container);

				var serializedId = values.Length > 1 ? values[1] : string.Empty;

				var showMembers = false;
				var triedCollectingMembers = false;
				List<MemberInfo> members = default;
				const int offsetLeft = 16;


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
						// using (new GUILayout.HorizontalScope())
						// {
						// 	GUILayout.Space(offsetLeft);
						// 	EditorGUILayout.LabelField("Experimental", EditorStyles.boldLabel);
						// }
						RenderCandidates();
						GUILayout.Space(8);
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


				var didSearchCandidates = false;
				var isExpanded = new Func<bool>(() => SessionState.GetBool("ShowCandidates_" + typeInfo, false));
				var setExpanded = new Action<bool>(s => SessionState.SetBool("ShowCandidates_" + typeInfo, s));
				List<ScriptCandidate> candidates = default;

				void RenderCandidates()
				{
					try
					{
						using (new GUILayout.HorizontalScope())
						{
							GUILayout.Space(offsetLeft);
							var prev = isExpanded();
							var show = EditorGUILayout.Foldout(prev, new GUIContent("Candidates (experimental)", "When opened it attempts to find similar types in relevant assemblies"));
							if (prev != show)
								setExpanded(show);
							if (!show) return;
						}

						if (!didSearchCandidates)
						{
							didSearchCandidates = true;

							if (!candidatesPerType.TryGetValue(typeInfo, out candidates))
								Utils.TryFindCandidatesInAssembly(typeInfo, out candidates);
						}

						if (candidates != null && candidates.Count > 0)
						{
							EditorGUI.indentLevel += 1;
							foreach (var c in candidates)
							{
								using (new GUILayout.HorizontalScope())
								{
									GUILayout.Space(offsetLeft);
									// using (new GUIColorScope(Color.Lerp(Color.white, Color.gray, c.Distance01 * .3f)))
									EditorGUILayout.ObjectField(new GUIContent(c.Distance + ":" + c.Type.Name, c.FilePath), c.Asset, typeof(Object), false);
								}
							}
							EditorGUI.indentLevel -= 1;
						}
						else
						{
							using (new GUILayout.HorizontalScope())
							{
								GUILayout.Space(offsetLeft);
								EditorGUI.indentLevel += 1;
								using (new EditorGUI.DisabledScope(true))
									EditorGUILayout.LabelField("None found");
								EditorGUI.indentLevel -= 1;
							}
						}
					}
					catch (ExitGUIException)
					{
					}
				}

				container.onGUIHandler += OnGUI;
			}
		}
	}
}