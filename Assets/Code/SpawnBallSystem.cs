using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace QuickSaveDemo
{
    public partial class SpawnBallSystem : SystemBase
    {
        private Entity _poolEntity;
        private Transform _cameraTransform;
        private int _amountToSpawn = 3;
        
        private ComponentLookup<FixedSizePool> _poolLookup;
        private BufferLookup<FixedSizePoolElement> _poolElementLookup;
        private EntityCommandBufferSystem _ecbSystem;

        protected override void OnCreate()
        {
            _poolLookup = GetComponentLookup<FixedSizePool>();
            _poolElementLookup = GetBufferLookup<FixedSizePoolElement>();
            _ecbSystem = World.GetOrCreateSystemManaged<BeginInitializationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (!EntityManager.Exists(_poolEntity))
                {
                    _poolEntity = EntityManager.CreateEntityQuery(typeof(FixedSizePool)).GetSingletonEntity();
                }

                if (_cameraTransform == null && Camera.main != null)
                {
                    _cameraTransform = Camera.main.transform;
                }

                if (_poolEntity != Entity.Null && _cameraTransform != null)
                {
                    _poolLookup.Update(this);
                    _poolElementLookup.Update(this);
                    Dependency = new SpawnJob()
                    {
                        PoolEntity = _poolEntity,
                        PoolLookup = _poolLookup,
                        PoolElementLookup = _poolElementLookup,
                        Ecb = _ecbSystem.CreateCommandBuffer(),
                        
                        AmountToSpawn = _amountToSpawn,
                        CameraPosition = _cameraTransform.position,
                        CameraForward = _cameraTransform.forward,
                        CameraRight = _cameraTransform.transform.right
                    }.Schedule(Dependency);
                    _ecbSystem.AddJobHandleForProducer(Dependency);
                    _amountToSpawn = math.min(_amountToSpawn + 2, 32);
                }
            }
        }
        
        private struct SpawnJob : IJob
        {
            public Entity PoolEntity;
            public ComponentLookup<FixedSizePool> PoolLookup;
            public BufferLookup<FixedSizePoolElement> PoolElementLookup;
            public EntityCommandBuffer Ecb;

            public int AmountToSpawn;
            public float3 CameraPosition;
            public float3 CameraForward;
            public float3 CameraRight;
            
            public void Execute()
            {
                FixedSizePool fixedSizePool = PoolLookup[PoolEntity];
                DynamicBuffer<FixedSizePoolElement> poolElements = PoolElementLookup[PoolEntity];
                AmountToSpawn = math.min(AmountToSpawn, poolElements.Length);
                
                Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex((uint)AmountToSpawn);
                float3 pos = CameraPosition + CameraForward * math.clamp(AmountToSpawn * 0.7f, 3f, 50f);
                float3 vel = CameraForward * 10f;
                LocalTransform localTransform = LocalTransform.Identity;
                
                for (int i = 0; i < AmountToSpawn; i++)
                {
                    localTransform.Position = pos + random.NextFloat3(new float3(-0.3f), new float3(0.3f));
                    var velComp = new PhysicsVelocity()
                    {
                        Linear = vel - CameraForward*math.clamp((i+1)*0.3f, 0f, 9f)
                    };
                    
                    // Actual Spawning
                    int indexInBuffer = (fixedSizePool.CurrentIndex + i) % poolElements.Length;
                    Entity entityToSpawn = poolElements[indexInBuffer].PooledEntity;
                    Ecb.RemoveComponent<Disabled>(entityToSpawn);
                    Ecb.SetComponent(entityToSpawn, localTransform);
                    Ecb.SetComponent(entityToSpawn, velComp);
                    
                    pos += CameraRight * (i % 2 == 0 ? (float) (i+1) * 0.75f : (float) (i+1) * -0.75f);
                }

                fixedSizePool.CurrentIndex += AmountToSpawn;
                PoolLookup[PoolEntity] = fixedSizePool;
            }
        }
    }
}