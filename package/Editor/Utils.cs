using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_2021_3_OR_NEWER || UNITY_2022_1_OR_NEWER
using UnityEditor.SceneManagement; // PrefabStageUtility
#else 
using UnityEditor.Experimental.SceneManagement; // PrefabStageUtility
#endif

// ReSharper disable CheckNamespace

namespace Needle.ComponentExtension
{
	internal class MemberInfo
	{
		public string Name;
		public string Value;
		public SerializedProperty Property;
	}

	public static class Utils
	{
		internal static void CollectMembersInfo(Object obj, string identifier, SerializedObject serializedObject, out List<MemberInfo> members)
		{
			members = null;
			if (string.IsNullOrEmpty(identifier)) return;
			string path = null;
			string[] lines = null;
			if (IsInPrefabState(out path))
			{
			}
			else
			{
				if (PrefabUtility.IsPartOfPrefabInstance(obj)) return;
				var scene = SceneManager.GetActiveScene();
				path = scene.path;
			}

			if (path != null)
				lines = File.ReadAllLines(path);

			if (lines?.Length > 0)
			{
				// TODO: this is the naive version, a component could be multiple times on an object and we also need to check for the gameobject id first and find the correct component etc
				var foundStart = false;
				foreach (var line in lines)
				{
					if (foundStart && line.StartsWith("---")) break;
					if (foundStart)
					{
						if (!line.Contains(":")) continue;
						members ??= new List<MemberInfo>();
						var member = new MemberInfo();
						members.Add(member);
						var values = line.Split(':');
						member.Name = values[0].Trim();
						member.Value = values[1].Trim();
						member.Property = serializedObject.FindProperty(member.Name);
					}
					else if (line.Contains(identifier))
					{
						foundStart = true;
					}
				}
			}
		}

		internal static bool CanShowProperties(Object obj)
		{
			var isInPrefab = IsInPrefabState(out _);
			return isInPrefab || !PrefabUtility.IsPartOfPrefabInstance(obj);
		}

		private static bool IsInPrefabState(out string path)
		{
			path = null;
			var stage = PrefabStageUtility.GetCurrentPrefabStage();
			if (stage) path = stage.assetPath;
			return stage;
		}
	}
}