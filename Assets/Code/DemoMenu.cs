// Author: Jonas De Maeseneer

using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace QuickSaveDemo
{
    public class DemoMenu : MonoBehaviour
    {
        private DemoSystem _demoSystem;
        [SerializeField] private GameObject _horizontalGroupPrefab;
        [SerializeField] private GameObject _buttonPrefab;

        private bool _showFullControls = false;
        private readonly List<Transform> _groupsOnlyInFull = new List<Transform>(8);

        private bool _buttonsInteractable = true;
        private readonly List<Button> _allButtons = new List<Button>(8);

        // Start is called before the first frame update
        private void Awake()
        {
            _demoSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<DemoSystem>();

            var currentGroup = AddHorizontalGroup(false);
            AddButton(currentGroup, "Load & Apply All", DemoSystem.Action.LoadAndApplyToScene, -1, true);
            AddButton(currentGroup, "Persist & Unload All", DemoSystem.Action.UnloadAndSaveScene, -1, true);
            AddButton(currentGroup, "Reset All", DemoSystem.Action.Reset, -1, true);

            currentGroup = AddHorizontalGroup(true);
            AddButton(currentGroup, $"(1) Load & Apply", DemoSystem.Action.LoadAndApplyToScene, 0);
            AddButton(currentGroup, $"(1) ToContainer", DemoSystem.Action.SceneToContainer, 0);
            AddButton(currentGroup, $"(1) ToFile", DemoSystem.Action.SceneContainerToFile, 0);
            AddButton(currentGroup, $"(1) FromFile", DemoSystem.Action.SceneContainerFromFile, 0);
            AddButton(currentGroup, $"(1) ToScene", DemoSystem.Action.ContainerToScene, 0);
            AddButton(currentGroup, $"(1) Persist & Unload", DemoSystem.Action.UnloadAndSaveScene, 0);
            AddButton(currentGroup, $"(1) ApplyInitial", DemoSystem.Action.InitialContainerToScene, 0);
            AddButton(currentGroup, $"(1) Reset", DemoSystem.Action.Reset, 0);

            currentGroup = AddHorizontalGroup(true);
            AddButton(currentGroup, $"(2) Load & Apply", DemoSystem.Action.LoadAndApplyToScene, 1);
            AddButton(currentGroup, $"(2) ToContainer", DemoSystem.Action.SceneToContainer, 1);
            AddButton(currentGroup, $"(2) ToFile", DemoSystem.Action.SceneContainerToFile, 1);
            AddButton(currentGroup, $"(2) FromFile", DemoSystem.Action.SceneContainerFromFile, 1);
            AddButton(currentGroup, $"(2) ToScene", DemoSystem.Action.ContainerToScene, 1);
            AddButton(currentGroup, $"(2) Persist & Unload", DemoSystem.Action.UnloadAndSaveScene, 1);
            AddButton(currentGroup, $"(2) ApplyInitial", DemoSystem.Action.InitialContainerToScene, 1);
            AddButton(currentGroup, $"(2) Reset", DemoSystem.Action.Reset, 1);

            currentGroup = AddHorizontalGroup(true);
            AddButton(currentGroup, $"(3) Load & Apply", DemoSystem.Action.LoadAndApplyToScene, 2);
            AddButton(currentGroup, $"(3) ToContainer", DemoSystem.Action.SceneToContainer, 2);
            AddButton(currentGroup, $"(3) ToFile", DemoSystem.Action.SceneContainerToFile, 2);
            AddButton(currentGroup, $"(3) FromFile", DemoSystem.Action.SceneContainerFromFile, 2);
            AddButton(currentGroup, $"(3) ToScene", DemoSystem.Action.ContainerToScene, 2);
            AddButton(currentGroup, $"(3) Persist & Unload", DemoSystem.Action.UnloadAndSaveScene, 2);
            AddButton(currentGroup, $"(3) ApplyInitial", DemoSystem.Action.InitialContainerToScene, 2);
            AddButton(currentGroup, $"(3) Reset", DemoSystem.Action.Reset, 2);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                _showFullControls = !_showFullControls;
                foreach (var t in _groupsOnlyInFull)
                {
                    t.gameObject.SetActive(_showFullControls);
                }
            }

            if (_buttonsInteractable != _demoSystem.Enabled)
            {
                _buttonsInteractable = _demoSystem.Enabled;
                foreach (var allButton in _allButtons)
                {
                    allButton.interactable = _buttonsInteractable;
                }
            }
        }

        private Transform AddHorizontalGroup(bool showOnlyOnFull)
        {
            Transform t = Instantiate(_horizontalGroupPrefab, transform).transform;
            if (showOnlyOnFull)
            {
                _groupsOnlyInFull.Add(t);
                t.gameObject.SetActive(false);
            }

            return t;
        }

        private void AddButton(Transform parent, string text, DemoSystem.Action action, int sceneIndex, bool big = false)
        {
            Button button = Instantiate(_buttonPrefab, parent).GetComponent<Button>();
            _allButtons.Add(button);
            button.onClick.AddListener(() =>
            {
                if (sceneIndex == -1)
                {
                    var list = new List<int>();
                    for (int i = 0; i < _demoSystem.AmountSubScenes; i++)
                    {
                        list.Add(i);
                    }

                    _demoSystem.SetCurrentAction(action, list);
                }
                else
                {
                    _demoSystem.SetCurrentAction(action, new List<int>() {sceneIndex});
                }
            });

            var textChild = button.GetComponentInChildren<Text>();
            textChild.text = text;
            if (!big)
            {
                textChild.fontSize = 20;
            }
        }
    }
}