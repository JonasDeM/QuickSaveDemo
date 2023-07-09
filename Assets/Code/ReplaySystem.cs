using QuickSave;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Scenes;
using UnityEngine;

namespace QuickSaveDemo
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SceneSystemGroup))]
    [UpdateAfter(typeof(QuickSaveSceneSystem))]
    [UpdateBefore(typeof(QuickSaveBeginFrameSystem))]
    public partial class ReplaySystem : SystemBase
    {
        public struct ReplayContainer : IBufferElementData
        {
            public Entity Container;
        }

        private const int AmountFrames = 256;
        
        private bool _paused;
        private int _frameCounter;
        public int RewindCounter => _rewindCounter;
        private int _rewindCounter;
        public int MaxRewind => _maxRewind;
        private int _maxRewind = 0;

        private EntityCommandBufferSystem _ecbSystem;
        private BufferLookup<DataTransferRequest> _requestLookup;
        private BufferLookup<ReplayContainer> _replayContainerLookup;
        private ComponentLookup<IsSectionLoaded> _sectionLoadedLookup;
        private BufferLookup<QuickSaveDataContainer.Data> _dataLookup;

        private SystemHandle _quickSaveBeginFrameSystem;
        private SystemHandle _quickSaveEndFrameSystem;

        private bool _unPauseRequested = false;
        private int _rewindCounterRequested = -1;

        public void RequestUnPause()
        {
            if (!_unPauseRequested && _rewindCounterRequested == -1)
                _unPauseRequested = true;
            else
                Debug.LogError("A request was already done this frame (Ignoring request)");
        }
        
        public void RequestSetRewindCounter(int counter)
        {
            if (!_unPauseRequested && _rewindCounterRequested == -1)
                _rewindCounterRequested = counter;
            else
                Debug.LogError("A request was already done this frame (Ignoring request)");
        }

        protected override void OnCreate()
        {
            _ecbSystem = World.GetOrCreateSystemManaged<EndInitializationEntityCommandBufferSystem>();
            _requestLookup = GetBufferLookup<DataTransferRequest>();
            _replayContainerLookup = GetBufferLookup<ReplayContainer>();
            _sectionLoadedLookup = GetComponentLookup<IsSectionLoaded>(true);
            _dataLookup = GetBufferLookup<QuickSaveDataContainer.Data>();

            _quickSaveBeginFrameSystem = World.GetOrCreateSystem<QuickSaveBeginFrameSystem>();
            _quickSaveEndFrameSystem = World.GetOrCreateSystem<QuickSaveEndFrameSystem>();
        }

        protected override void OnUpdate()
        {
            // Action to system changes
            if (_unPauseRequested)
            {
                _paused = false;
                _frameCounter -= _rewindCounter; // reset the framecounter to the 'past'
                _maxRewind -= _rewindCounter; // since we're now in the 'past' we need to set/limit the amount of frames we can still rewind
                _rewindCounter = 0;
                SetSimulationSystemsEnabled(true);
            }
            else if (_rewindCounterRequested != -1)
            {
                _paused = true;
                _rewindCounter = math.clamp(_rewindCounterRequested, 0, _maxRewind);
                SetSimulationSystemsEnabled(false);
            }
            _unPauseRequested = false;
            _rewindCounterRequested = -1;
            
            // Update Frame Counter
            if (!_paused)
            {
                _frameCounter++;
                _maxRewind = math.min(_maxRewind + 1, AmountFrames - 1);
            }

            // Action to entity changes
            _requestLookup.Update(this);
            _replayContainerLookup.Update(this);
            _sectionLoadedLookup.Update(this);
            _dataLookup.Update(this);
            new ReplayJob()
            {
                Ecb = _ecbSystem.CreateCommandBuffer(),
                DataTransferRequestLookup = _requestLookup,
                ReplayContainerLookup = _replayContainerLookup,
                SectionLoadedLookup = _sectionLoadedLookup,
                DataLookup = _dataLookup,
                
                Pause = _paused,
                CurrentFrame = _frameCounter,
                RewindCounter = _rewindCounter,
                
                QuickSaveBeginFrameSystem = _quickSaveBeginFrameSystem,
                QuickSaveEndFrameSystem = _quickSaveEndFrameSystem
            }.Schedule();
            
            _ecbSystem.AddJobHandleForProducer(Dependency);
        }
        
        // An alternative would be to apply the state after all simulation systems ran,
        // but I prefer explicitly disabling systems that shouldn't run when the game is paused
        private void SetSimulationSystemsEnabled(bool enabled)
        {
            World.GetOrCreateSystemManaged<PhysicsSimulationGroup>().Enabled = enabled;
            World.GetOrCreateSystemManaged<DemoSystem>().Enabled = enabled;
        }

        [BurstCompile]
        private partial struct ReplayJob : IJobEntity
        {
            public EntityCommandBuffer Ecb;
            public BufferLookup<DataTransferRequest> DataTransferRequestLookup;
            public BufferLookup<ReplayContainer> ReplayContainerLookup;
            [ReadOnly]
            public ComponentLookup<IsSectionLoaded> SectionLoadedLookup;
            public BufferLookup<QuickSaveDataContainer.Data> DataLookup;

            public bool Pause;
            public int CurrentFrame;
            public int RewindCounter;

            public SystemHandle QuickSaveBeginFrameSystem;
            public SystemHandle QuickSaveEndFrameSystem;
            
            public void Execute(Entity e, in QuickSaveSceneSection quickSaveSceneSection)
            {
                // Handle scenes that don't have replay containers yet
                if (!ReplayContainerLookup.TryGetBuffer(e, out DynamicBuffer<ReplayContainer> replayContainer))
                {
                    replayContainer = Ecb.AddBuffer<ReplayContainer>(e);
                    replayContainer.Resize(AmountFrames, NativeArrayOptions.ClearMemory);
                }

                Entity initialContainerEntity = quickSaveSceneSection.InitialStateContainerEntity;

                if (Pause) // Apply the state of the paused frame
                {
                    var request = new DataTransferRequest
                    {
                        ExecutingSystem = QuickSaveBeginFrameSystem,
                        RequestType = DataTransferRequest.Type.FromDataContainerToEntities
                    };
                    int indexToApply = (CurrentFrame - RewindCounter) % AmountFrames;
                    Entity containerEntity = replayContainer[indexToApply].Container;

                    if (containerEntity != default)
                    {
                        DataTransferRequestLookup[containerEntity].Add(request);
                    }
                    else
                    {
                        DataTransferRequestLookup[initialContainerEntity].Add(request);
                    }
                }
                else // Save the state of the current frame
                {
                    var request = new DataTransferRequest
                    {
                        ExecutingSystem = QuickSaveEndFrameSystem,
                        RequestType = DataTransferRequest.Type.FromEntitiesToDataContainer
                    };
                    int indexToSaveInto = CurrentFrame % AmountFrames;
                    Entity containerEntity = replayContainer[indexToSaveInto].Container;
                    
                    if (SectionLoadedLookup.HasComponent(e))
                    {
                        if (containerEntity != default)
                        {
                            // If scene is loaded & a container exists for this frame
                            DataTransferRequestLookup[containerEntity].Add(request);
                        }
                        else
                        {
                            // If scene is loaded & no container exists for this frame
                            Entity newContainerEntity = Ecb.Instantiate(initialContainerEntity);
                            Ecb.AppendToBuffer(newContainerEntity, request);

                            var replayBuffer = Ecb.SetBuffer<ReplayContainer>(e);
                            replayBuffer.CopyFrom(replayContainer);
                            replayBuffer[indexToSaveInto] = new ReplayContainer { Container = newContainerEntity };
                        }
                    }
                    else
                    {
                        int indexToCopyFrom = (CurrentFrame-1) % AmountFrames;
                        Entity prevFrameContainer = replayContainer[indexToCopyFrom].Container;
                        
                        if (containerEntity != default)
                        {
                            // If scene is not loaded & a container exists for this frame
                            var prevFrameData = DataLookup[prevFrameContainer];
                            var data = DataLookup[containerEntity];
                            data.CopyFrom(prevFrameData);
                        }
                        else
                        {
                            // If scene is not loaded & no container exists for this frame
                            Entity newContainerEntity = Ecb.Instantiate(prevFrameContainer);

                            var replayBuffer = Ecb.SetBuffer<ReplayContainer>(e);
                            replayBuffer.CopyFrom(replayContainer);
                            replayBuffer[indexToSaveInto] = new ReplayContainer { Container = newContainerEntity };
                        }
                    }
                }
            }
        }
    }
}