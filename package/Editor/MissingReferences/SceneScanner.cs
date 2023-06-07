using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;

#if UNITY_2021_2_OR_NEWER
using UnityEditor.SceneManagement;
#else
using UnityEditor.Experimental.SceneManagement;
#endif

namespace Needle.MissingReferences
{
    public class SceneScanner
    {
        [Serializable]
        public class Options
        {
            public bool IncludeEmptyEvents = true;
            public bool IncludeMissingMethods = true;
            public bool IncludeUnsetMethods = true;
        }
        
        const string k_PersistentCallsSearchString = "m_PersistentCalls.m_Calls.Array.data[";
        const string k_TargetPropertyName = "m_Target";
        const string k_MethodNamePropertyName = "m_MethodName";
        const string k_CallStatePropertyName = "m_CallState";
        const string k_UntitledSceneName = "Untitled";
        
        readonly List<KeyValuePair<string, GameObjectContainer>> m_SceneRoots = new List<KeyValuePair<string, GameObjectContainer>>();
        readonly Options options;
        readonly List<GameObjectContainer> allMissingReferencesContainers = new List<GameObjectContainer>();

        public List<GameObjectContainer> MissingReferences => allMissingReferencesContainers;
        public List<KeyValuePair<string, GameObjectContainer>> SceneRoots => m_SceneRoots;

        public SceneScanner(Options options)
        {
            m_SceneRoots.Clear();
            this.options = options;
        }
        
        public bool FindMissingReferences()
        {
            // If we are in prefab isolation mode, scan the prefab stage instead of the active scene
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                ScanScene(prefabStage.scene, options, m_SceneRoots);
            }

            var loadedSceneCount = SceneManager.sceneCount;
            for (var i = 0; i < loadedSceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid())
                    continue;

                ScanScene(scene, options, m_SceneRoots);
            }
            
            allMissingReferencesContainers.Clear();

            void AddToList(List<GameObjectContainer> list, GameObjectContainer container)
            {
                list.AddRange(container.Children.Where(x => x.HasMissingReferences));
                foreach (var child in container.Children)
                {
                    AddToList(list, child);
                }
            }

            foreach (var kvp in m_SceneRoots)
            {
                if (kvp.Value.HasMissingReferences)
                    allMissingReferencesContainers.Add(kvp.Value);
                AddToList(allMissingReferencesContainers, kvp.Value);
            }

            return allMissingReferencesContainers.Any();
        }
        
        static void ScanScene(Scene scene, Options options, List<KeyValuePair<string, GameObjectContainer>> m_SceneRoots)
        {
            var rootObjectContainer = new GameObjectContainer();
            foreach (var gameObject in scene.GetRootGameObjects())
            {
                rootObjectContainer.AddChild(gameObject, options);
            }

            rootObjectContainer.CheckRenderSettings(options);
            rootObjectContainer.CheckLightingSettings(options);
            
            if (rootObjectContainer.Count > 0)
            {
                var sceneName = scene.name;
                if (string.IsNullOrEmpty(sceneName))
                    sceneName = k_UntitledSceneName;

                m_SceneRoots.Add(new KeyValuePair<string, GameObjectContainer>(sceneName, rootObjectContainer));
            }
            
        }
        
        /// <summary>
        /// Check a UnityObject for missing serialized references
        /// </summary>
        /// <param name="obj">The UnityObject to be scanned</param>
        /// <param name="properties">A list to which properties with missing references will be added</param>
        /// <param name="options">User-configurable options for this view</param>
        /// <returns>True if the object has any missing references</returns>
        public static void CheckForMissingReferences(UnityObject obj, List<SerializedProperty> properties, Options options)
        {
            if (obj == null)
                return;

            var property = new SerializedObject(obj).GetIterator();
            while (property.NextVisible(true)) // enterChildren = true to scan all properties
            {
                if (CheckForMissingReferences(property, options))
                    properties.Add(property.Copy()); // Use a copy of this property because we are iterating on it
            }
        }
        
        public static void CheckForMissingReferences(SerializedObject obj, List<SerializedProperty> properties, Options options)
        {
            if (obj == null)
                return;

            var property = obj.GetIterator();
            while (property.NextVisible(true)) // enterChildren = true to scan all properties
            {
                if (CheckForMissingReferences(property, options))
                    properties.Add(property.Copy()); // Use a copy of this property because we are iterating on it
            }
        }

        static bool CheckForMissingReferences(SerializedProperty property, Options options)
        {
            var propertyPath = property.propertyPath;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Generic:
                    var includeEmptyEvents = options.IncludeEmptyEvents;
                    var includeUnsetMethods = options.IncludeUnsetMethods;
                    var includeMissingMethods = options.IncludeMissingMethods;
                    if (!includeEmptyEvents && !includeUnsetMethods && !includeMissingMethods)
                        return false;

                    // Property paths matching a particular pattern will contain serialized UnityEvent references
                    if (propertyPath.Contains(k_PersistentCallsSearchString))
                    {
                        // UnityEvent properties contain a target object and a method name
                        var targetProperty = property.FindPropertyRelative(k_TargetPropertyName);
                        var methodProperty = property.FindPropertyRelative(k_MethodNamePropertyName);
                        var callState = property.FindPropertyRelative(k_CallStatePropertyName);

                        if (targetProperty != null && methodProperty != null)
                        {
                            // if this event is turned OFF we don't need to return it
                            if (callState != null && callState.enumValueIndex == 0) 
                                return false;
                            
                            // If the target reference is missing, we can't search for methods. If the user has chosen
                            // to include empty events, we return true, otherwise we must ignore this event.
                            if (targetProperty.objectReferenceValue == null)
                            {
                                // If the target is a missing reference it will be caught below
                                if (targetProperty.objectReferenceInstanceIDValue != 0)
                                    return false;

                                return includeEmptyEvents;
                            }

                            var methodName = methodProperty.stringValue;
                            // Include if the method name is empty and the user has chosen to include unset methods
                            if (string.IsNullOrEmpty(methodName))
                                return includeUnsetMethods;

                            if (includeMissingMethods)
                            {
                                // If the user has chosen to include missing methods, check if the target object type
                                // for public methods with the same name as the value of the method property
                                var type = targetProperty.objectReferenceValue.GetType();
                                try
                                {
                                    if (!type.GetMethods().Any(info => info.Name == methodName))
                                        return true;
                                }
                                catch (Exception e)
                                {
                                    Debug.LogException(e);

                                    // Treat reflection errors as missing methods
                                    return true;
                                }
                            }
                        }
                    }
                    break;
                case SerializedPropertyType.ObjectReference:
                    // Some references may be null, which is to be expected--not every field is set
                    // Valid asset references will have a non-null objectReferenceValue
                    // Valid asset references will have some non-zero objectReferenceInstanceIDValue value
                    // References to missing assets will have a null objectReferenceValue, but will retain
                    // their non-zero objectReferenceInstanceIDValue
                    if (property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                        return true;

                    break;
            }

            return false;
        }
    }
}