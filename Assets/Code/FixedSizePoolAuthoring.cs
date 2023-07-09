using System.Collections.Generic;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace QuickSaveDemo
{
    public struct FixedSizePool : IComponentData
    {
        public int CurrentIndex;
    }
    
    public struct FixedSizePoolElement : IBufferElementData
    {
        public Entity PooledEntity;
    }

    public class FixedSizePoolAuthoring : MonoBehaviour
    {
        public GameObject Prefab;

        public List<GameObject> GetPoolEntries(out List<string> setupErrors)
        {
#if UNITY_EDITOR
            var entries = new List<GameObject>();
            setupErrors = new List<string>();
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(Prefab, out string prefabGUID, out long _);
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGUID);
            
            for (int i = 0; i < transform.childCount; i++)
            {
                // Some validations to make sure the setup was as expected.
                GameObject child = transform.GetChild(i).gameObject;
                if (child.activeSelf)
                {
                    setupErrors.Add("Child of this fixed size pool was not disabled!");
                    continue;
                }

                if (!PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
                {
                    setupErrors.Add($"Child of this fixed size pool was not a prefab instance!");
                    continue;
                }

                string childPrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject);
                if (childPrefabPath != prefabPath)
                {
                    setupErrors.Add($"Child of this fixed size pool was not an instance of the prefab '{Prefab.name}'");
                    continue;
                }
                
                entries.Add(child);
            }

            return entries;
#else
            throw new System.NotSupportedException("This function is only supported in Editor");
#endif
        }
    }
    
    public class FixedSizePoolBaker : Baker<FixedSizePoolAuthoring>
    {
        public override void Bake(FixedSizePoolAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            DynamicBuffer<FixedSizePoolElement> pool = AddBuffer<FixedSizePoolElement>(entity);

            foreach (var entry in authoring.GetPoolEntries(out _))
            {
                pool.Add(new FixedSizePoolElement() { PooledEntity = GetEntity(entry, TransformUsageFlags.None)});
            }
            
            AddComponent<FixedSizePool>(entity);
        }
    }
}