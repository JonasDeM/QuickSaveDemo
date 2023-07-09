using QuickSave;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace QuickSaveDemo
{
    public partial class StatsSystem : SystemBase
    {
        public ContainerStatistics Statistics { get; private set; }

        private NativeReference<ContainerStatistics> _statsInJob;
        private NativeHashSet<Hash128> _containerIds;

        protected override void OnCreate()
        {
            _statsInJob = new NativeReference<ContainerStatistics>(Allocator.Persistent);
            _containerIds = new NativeHashSet<Hash128>(16, Allocator.Persistent);
        }

        protected override void OnUpdate()
        {
            Statistics = _statsInJob.Value;
            _statsInJob.Value = default;
            _containerIds.Clear();
            new GatherStatisticsJob()
            {
                Stats = _statsInJob,
                ContainerIds = _containerIds
            }.Schedule();
        }

        protected override void OnDestroy()
        {
            _statsInJob.Dispose();
            _containerIds.Dispose();
        }

        public struct ContainerStatistics
        {
            public long TotalByteCount;
            public int TotalTrackedEntities;
            public int TotalAmountContainers;
            public int TotalAmountUniqueContainerIds;
        }
        
        [BurstCompile]
        private partial struct GatherStatisticsJob : IJobEntity
        {
            public NativeReference<ContainerStatistics> Stats;
            
            public NativeHashSet<Hash128> ContainerIds;
            
            public void Execute(in QuickSaveDataContainer container, DynamicBuffer<QuickSaveArchetypeDataLayout> dataLayouts, DynamicBuffer<QuickSaveDataContainer.Data> data)
            {
                var stats = Stats.Value;
                
                stats.TotalByteCount += data.Length;
                stats.TotalAmountContainers += 1;
                if (ContainerIds.Add(container.GUID))
                {
                    stats.TotalAmountUniqueContainerIds += 1;
                    
                    for (int i = 0; i < dataLayouts.Length; i++)
                    {
                        stats.TotalTrackedEntities += dataLayouts[i].Amount;
                    }
                }

                Stats.Value = stats;
            }
        }
    }
}
