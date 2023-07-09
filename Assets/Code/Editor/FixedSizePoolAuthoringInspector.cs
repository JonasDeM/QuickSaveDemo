using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace QuickSaveDemo
{
    [CustomEditor(typeof(FixedSizePoolAuthoring))]
    public class FixedSizePoolAuthoringInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            FixedSizePoolAuthoring pool = (FixedSizePoolAuthoring)target;

            List<GameObject> entries = pool.GetPoolEntries(out List<string> setupErrors);

            if (pool.Prefab != null)
            {
                int newSize = EditorGUILayout.IntField("PoolSize", entries.Count);
                if (newSize != entries.Count)
                {
                    int childCount = pool.transform.childCount;
                    for (int i = 0; i < childCount; i++)
                    {
                        GameObject child = pool.transform.GetChild(0).gameObject;
                        DestroyImmediate(child);
                    }

                    for (int i = 0; i < newSize; i++)
                    {
                        GameObject newInstance = (GameObject)PrefabUtility.InstantiatePrefab(pool.Prefab, pool.transform);
                        newInstance.gameObject.SetActive(false);
                    }
                    
                    EditorSceneManager.MarkSceneDirty(pool.gameObject.scene);
                }
            }
            else
            {
                GUI.enabled = false;
                EditorGUILayout.IntField("PoolSize", 0);
                GUI.enabled = true;
            }

            foreach (var error in setupErrors.Distinct())
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }
    }
}