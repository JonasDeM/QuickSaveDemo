// Author: Jonas De Maeseneer

using System.Collections.Generic;
using System.IO;
using System.Text;
using QuickSave;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Scenes;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;
using RaycastHit = Unity.Physics.RaycastHit;

// This DemoSystem intentionally does everything via EntityManager to make it easier to understand the API
// It does not focus on optimal performance, but rather focuses on explaining the API
namespace QuickSaveDemo
{
    public partial class DemoSystem : SystemBase
    {
        // Members
        // *******
        
        public enum Action
        {
            Nothing,
            LoadAndApplyToScene,
            UnloadAndSaveScene,
            SceneToContainer,
            ContainerToScene,
            SceneContainerToFile,
            SceneContainerFromFile,
            InitialContainerToScene,
            Reset
        }

        private EntityCommandBufferSystem _ecbSystem;
        private Camera _camera;

        private bool _initSubScene = true;

        public int AmountSubScenes => _subScenes.Length;
        private NativeList<DemoSubScenes> _subScenes;

        private struct DemoSubScenes
        {
            public Entity Entity;
            public Hash128 Guid;
            public Entity InitialContainer;
            public Entity LastSavedContainer;
        }

        private NativeList<int> _sceneContainersToUpdate;

        private (Action, List<int>) _currentAction = (Action.Nothing, null);

        private const string SaveFolder = "Demo_LastSaved";
        private readonly StringBuilder _pathStringBuilder = new StringBuilder(256);

        // Interesting Functions To Learn API
        // **********************************

        private void InitialContainerToScene(int index)
        {
            EntityManager.GetBuffer<DataTransferRequest>(_subScenes[index].InitialContainer).Add(new DataTransferRequest
            {
                ExecutingSystem = World.GetExistingSystem<QuickSaveBeginFrameSystem>(),
                RequestType = DataTransferRequest.Type.FromDataContainerToEntities
            });
        }

        private void SceneToContainer(int index)
        {
            EntityManager.GetBuffer<DataTransferRequest>(_subScenes[index].LastSavedContainer).Add(new DataTransferRequest
            {
                ExecutingSystem = World.GetExistingSystem<QuickSaveEndFrameSystem>(),
                RequestType = DataTransferRequest.Type.FromEntitiesToDataContainer
            });
        }

        private void ContainerToScene(int index)
        {
            EntityManager.GetBuffer<DataTransferRequest>(_subScenes[index].LastSavedContainer).Add(new DataTransferRequest
            {
                ExecutingSystem = World.GetExistingSystem<QuickSaveBeginFrameSystem>(),
                RequestType = DataTransferRequest.Type.FromDataContainerToEntities
            });
        }

        private void DeleteFile(int index)
        {
            string filePath = QuickSaveAPI.CalculatePathString(_pathStringBuilder, SaveFolder, _subScenes[index].Guid);
            File.Delete(filePath);
            File.Delete(filePath + ".meta"); // in editor
        }

        private bool FileExists(int index)
        {
            string filePath = QuickSaveAPI.CalculatePathString(_pathStringBuilder, SaveFolder, _subScenes[index].Guid);
            return File.Exists(filePath);
        }

        private void SceneContainerToFile(int index)
        {
            var subScene = _subScenes[index];
            EntityManager.AddComponentData(subScene.LastSavedContainer, new RequestSerialization()
            {
                FolderName = SaveFolder
            });
            EntityManager.SetComponentEnabled<RequestSerialization>(_subScenes[index].LastSavedContainer, true);
        }

        private void SceneContainerFromFile(int index, RequestDeserialization.ActionFlags postCompleteActions)
        {
            var subScene = _subScenes[index];
            var request = new RequestDeserialization
            {
                FolderName = SaveFolder,
                PostCompleteActions = postCompleteActions,
                PostCompleteActionSceneEntity = subScene.Entity
            };
            
            if (subScene.LastSavedContainer == Entity.Null)
            {
                subScene.LastSavedContainer = QuickSaveAPI.RequestDeserializeIntoNewUninitializedContainer(EntityManager, _subScenes[index].Guid, request);
                _subScenes[index] = subScene;
            }
            else
            {
                EntityManager.AddComponentData(subScene.LastSavedContainer, request);
            }

            EntityManager.SetComponentEnabled<RequestDeserialization>(subScene.LastSavedContainer, true);
            
            if ((postCompleteActions & RequestDeserialization.ActionFlags.RequestSceneLoaded) != 0)
                OnSceneLoadRequested(index);
        }

        private void UnloadScene(int index)
        {
            EntityManager.AddComponent<RequestSceneUnloaded>(_subScenes[index].Entity);
        }

        private void LoadScene(int index)
        {
            EntityManager.AddComponent<RequestSceneLoaded>(_subScenes[index].Entity);
            OnSceneLoadRequested(index);
        }
        
        private bool LatestContainerExistsAndValid(int index)
        {
            return _subScenes[index].LastSavedContainer != Entity.Null && EntityManager.HasComponent<DataTransferRequest>(_subScenes[index].LastSavedContainer);
        }

        private bool InitialContainerExistsAndValid(int index)
        {
            return _subScenes[index].InitialContainer != Entity.Null && EntityManager.HasComponent<DataTransferRequest>(_subScenes[index].InitialContainer);
        }

        // This is how the DemoSystem manages its 'LastSaved Containers', but a real game likely does it differently!
        private void UpdateContainers()
        {
            for (int i = 0; i < _sceneContainersToUpdate.Length; i++)
            {
                int sceneIndex = _sceneContainersToUpdate[i];
                var subSceneEntity = _subScenes[sceneIndex];

                if (!EntityManager.HasComponent<QuickSaveSceneSection>(subSceneEntity.Entity))
                    continue; // scene not yet loaded for the first time

                Debug.Assert(subSceneEntity.InitialContainer == Entity.Null);
                subSceneEntity.InitialContainer = EntityManager.GetComponentData<QuickSaveSceneSection>(subSceneEntity.Entity).InitialStateContainerEntity;

                // Uninitialized container could already have been instantiated & have become valid now that the scene is loaded.
                if (subSceneEntity.LastSavedContainer == Entity.Null)
                {
                    subSceneEntity.LastSavedContainer = QuickSaveAPI.InstantiateContainer(EntityManager, subSceneEntity.InitialContainer);
                }
                else if (!EntityManager.GetComponentData<QuickSaveDataContainer>(subSceneEntity.LastSavedContainer).ValidData)
                {
                    EntityManager.DestroyEntity(subSceneEntity.LastSavedContainer);
                    subSceneEntity.LastSavedContainer = QuickSaveAPI.InstantiateContainer(EntityManager, subSceneEntity.InitialContainer);
                }

                _subScenes[sceneIndex] = subSceneEntity;

                // Add these components (disabled) for easy (de)serialization by just enabling them. (Once executed they get disabled again automatically)
                EntityManager.AddComponentData(subSceneEntity.LastSavedContainer, new RequestSerialization {FolderName = SaveFolder});
                EntityManager.SetComponentEnabled<RequestSerialization>(subSceneEntity.LastSavedContainer, false);
                EntityManager.AddComponentData(subSceneEntity.LastSavedContainer, new RequestDeserialization {FolderName = SaveFolder});
                EntityManager.SetComponentEnabled<RequestDeserialization>(subSceneEntity.LastSavedContainer, false);

                // Add these components for easy auto-save & auto-load
                EntityManager.AddComponentData(subSceneEntity.Entity, new AutoApplyOnLoad {ContainerEntityToApply = subSceneEntity.LastSavedContainer});
                EntityManager.AddComponentData(subSceneEntity.Entity, new AutoPersistOnUnload {ContainerEntityToPersist = subSceneEntity.LastSavedContainer});

                _sceneContainersToUpdate.RemoveAtSwapBack(i);
            }
        }

        private void OnSceneLoadRequested(int sceneIndex)
        {
            if (_subScenes[sceneIndex].InitialContainer == Entity.Null)
                _sceneContainersToUpdate.Add(sceneIndex);
        }

        // Rest of Implementation
        // **********************

        protected override void OnCreate()
        {
            _ecbSystem = World.GetOrCreateSystemManaged<BeginInitializationEntityCommandBufferSystem>();
            _subScenes = new NativeList<DemoSubScenes>(4, Allocator.Persistent);
            _sceneContainersToUpdate = new NativeList<int>(4, Allocator.Persistent);

            QuickSaveSettings.Initialize();
            if (QuickSaveSettings.NothingToSave)
            {
                Enabled = false;
            }
        }
        
        protected override void OnUpdate()
        {
            bool initialized = InitLogic();
            if (!initialized)
                return;

            UpdateContainers();

            UpdateDemoActions();

            _ecbSystem.AddJobHandleForProducer(Dependency);
        }
        
        protected override void OnDestroy()
        {
            QuickSaveSettings.CleanUp();
            _subScenes.Dispose();
            _sceneContainersToUpdate.Dispose();
        }

        public void SetCurrentAction(Action action, List<int> sceneIndices)
        {
            if (_currentAction.Item1 == Action.Nothing && sceneIndices != null)
            {
                _currentAction = (action, sceneIndices);
            }
        }

        private void UpdateDemoActions()
        {
            if (_currentAction.Item2 != null && _currentAction.Item1 != Action.Nothing)
            {
                try
                {
                    foreach (int sceneIndex in _currentAction.Item2)
                    {
                        ExecuteAction(_currentAction.Item1, sceneIndex);
                    }
                }
                finally
                {
                    _currentAction = default;
                }
            }

            UpdateMouseActions();
        }

        private void UpdateMouseActions()
        {
            bool leftClick = Input.GetMouseButtonDown(0);
            bool rightClick = Input.GetMouseButtonDown(1);
            bool middleClick = Input.GetMouseButtonDown(2);
            if (leftClick || rightClick)
            {
                UnityEngine.Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
                Unity.Physics.RaycastInput physicsRay = new RaycastInput()
                {
                    Start = ray.origin,
                    End = ray.origin + ray.direction * 10000f,
                    Filter = CollisionFilter.Default
                };
                PhysicsWorldSingleton physicsWorld = EntityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton)).GetSingleton<PhysicsWorldSingleton>();
                physicsWorld.CastRay(physicsRay, out RaycastHit hit);

                // UnityEngine.Debug.DrawLine(physicsRay.Start, physicsRay.End, Color.red, 10, false);
                if (hit.Entity != Entity.Null)
                {
                    if (leftClick)
                        EntityManager.RemoveComponent<PhysicsVelocity>(hit.Entity);
                    if (rightClick)
                        EntityManager.AddComponent<Disabled>(hit.Entity);
                }
            }

            if (middleClick)
            {
                var restoreEcb = _ecbSystem.CreateCommandBuffer();
                Entities.WithAll<Disabled, LocalIndexInContainer>().ForEach((Entity e) => { restoreEcb.RemoveComponent<Disabled>(e); })
                    .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabledEntities)
                    .Schedule();
                var physicsRestoreEcb = _ecbSystem.CreateCommandBuffer();
                Entities.WithNone<PhysicsVelocity>().WithAll<PhysicsMass, PhysicsDamping>().ForEach((Entity e) =>
                    {
                        physicsRestoreEcb.AddComponent<PhysicsVelocity>(e);
                    }).WithEntityQueryOptions(EntityQueryOptions.IncludeDisabledEntities)
                    .Schedule();
            }
        }
        
        private bool IsSceneLoaded(int index)
        {
            return EntityManager.HasComponent<IsSectionLoaded>(_subScenes[index].Entity);
        }

        private void ExecuteAction(Action action, int sceneIndex)
        {
            switch (action)
            {
                case Action.LoadAndApplyToScene:
                    if (FileExists(sceneIndex))
                        SceneContainerFromFile(sceneIndex, RequestDeserialization.ActionFlags.All);
                    else
                        LoadScene(sceneIndex);
                    break;
                case Action.UnloadAndSaveScene:
                    if (IsSceneLoaded(sceneIndex))
                    {
                        UnloadScene(sceneIndex);
                        if (LatestContainerExistsAndValid(sceneIndex))
                            SceneContainerToFile(sceneIndex);
                    }
                    break;
                case Action.SceneToContainer:
                    if (LatestContainerExistsAndValid(sceneIndex))
                        SceneToContainer(sceneIndex);
                    break;
                case Action.ContainerToScene:
                    if (LatestContainerExistsAndValid(sceneIndex))
                        ContainerToScene(sceneIndex);
                    break;
                case Action.SceneContainerToFile:
                    if (LatestContainerExistsAndValid(sceneIndex))
                        SceneContainerToFile(sceneIndex);
                    break;
                case Action.SceneContainerFromFile:
                    if (FileExists(sceneIndex))
                        SceneContainerFromFile(sceneIndex, RequestDeserialization.ActionFlags.None);
                    break;
                case Action.InitialContainerToScene:
                    if (InitialContainerExistsAndValid(sceneIndex))
                        InitialContainerToScene(sceneIndex);
                    break;
                case Action.Reset:
                    if (InitialContainerExistsAndValid(sceneIndex))
                        InitialContainerToScene(sceneIndex);
                    DeleteFile(sceneIndex);
                    break;
            }
        }

        private bool InitLogic()
        {
            if (_initSubScene)
            {
                var listToFill = _subScenes;
                var containersToUpdate = _sceneContainersToUpdate;
                Entities.ForEach((Entity e, in SceneSectionData sceneSectionData) =>
                {
                    listToFill.Add(new DemoSubScenes() {Entity = e, Guid = sceneSectionData.SceneGUID});
                    if (SystemAPI.HasComponent<RequestSceneLoaded>(e))
                    {
                        containersToUpdate.Add(listToFill.Length - 1);
                    }
                }).Run();
                
                _initSubScene = false;
            }

            if (_camera == null)
            {
                if (Camera.main != null)
                {
                    _camera = Camera.main;
                }
            }

            return !_initSubScene && _camera != null;
        }
    }
}