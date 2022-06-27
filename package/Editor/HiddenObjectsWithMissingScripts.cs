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
			foreach (var go in gos)
			{
				comps.Clear();
				go.GetComponents(comps);
				foreach(var comp in comps)
				{
					if (comp == null)
					{
						go.hideFlags = HideFlags.None;
					}
				}
			}
		}
	}
}