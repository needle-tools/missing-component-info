using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Needle
{
	public static class HiddenObjectsWithMissingScripts
	{
		[MenuItem("Tools/Needle/Show Objects With Missing Scripts")]
		private static void ShowHiddenObjects()
		{
			var gos = Resources.FindObjectsOfTypeAll<GameObject>();
			var comps = new List<Component>();
			var previouslyHidden = new List<GameObject>();
			foreach (var go in gos)
			{
				comps.Clear();
				go.GetComponents(comps);
				foreach(var comp in comps)
				{
					if (comp == null)
					{
						previouslyHidden.Add(go);
						go.hideFlags = HideFlags.None;
					}
				}
			}
			// Select and log the objects that were hidden
			if (previouslyHidden.Count > 0)
			{
				// ReSharper disable once CoVariantArrayConversion
				Selection.objects = previouslyHidden.ToArray();
				Debug.Log(
					$"Found {previouslyHidden.Count} hidden objects with missing scripts:\n" +
					$"{string.Join("\n", previouslyHidden.ConvertAll(go => go.name).ToArray())}");
			}
		}
	}
}