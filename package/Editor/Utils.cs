using System.Collections.Generic;
using System.IO;
using UnityEditor;

using UnityEngine.SceneManagement;
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
		internal static void CollectMembersInfo(string id, string identifier, SerializedObject obj, out List<MemberInfo> members)
		{
			members = null;
			return;
			// string[] lines = null;
			// if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage())
			// {
			// 	// TODO: render in prefab stage
			// }
			// else
			// {
			// 	var scene = SceneManager.GetActiveScene();
			// 	lines = File.ReadAllLines(scene.path);
			// }
			//
			// if (lines != null)
			// {
			// 	// TODO: this is the naive version, a component could be multiple times on an object and we also need to check for the gameobject id first and find the correct component etc
			// 	var foundStart = false;
			// 	foreach (var line in lines)
			// 	{
			// 		if (foundStart && line.StartsWith("---")) break;
			// 		if (foundStart)
			// 		{
			// 			members ??= new List<MemberInfo>();
			// 			var member = new MemberInfo();
			// 			members.Add(member);
			// 			var values = line.Split(':');
			// 			member.Name = values[0].Trim();
			// 			member.Value = values[1].Trim();
			// 			member.Property = obj.FindProperty(member.Name);
			// 		}
			// 		else if (line.Contains(identifier))
			// 		{
			// 			foundStart = true;
			// 		}
			// 	}
			// }
		}
	}
}