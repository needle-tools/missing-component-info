#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Needle
{
    public class ForceReserializeAssets : MonoBehaviour
    {
        [MenuItem("MissingComponent Tests/Force Reserialize")]
        public static void ForceReserialize()
        {
            AssetDatabase.ForceReserializeAssets();
        }
    }
}
#endif  // UNITY_EDITOR