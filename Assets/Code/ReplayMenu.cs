// Author: Jonas De Maeseneer

using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace QuickSaveDemo
{
    public class ReplayMenu : MonoBehaviour
    {
        private ReplaySystem _replaySystem;
        [SerializeField] private Button _pausePlayButton;
        [SerializeField] private Image _pausePlayButtonImage;
        [SerializeField] private Sprite _pauseSprite;
        [SerializeField] private Sprite _playSprite;
        [SerializeField] private Slider _slider;
        [SerializeField] private Button _stopReplayModeButton;

        private bool _replayMode;
        private bool _playPausePressed;
        private bool _stopReplayModePressed;
        private bool _autoPlay;

        private void Start()
        {
            _replaySystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<ReplaySystem>();

            _pausePlayButton.onClick.AddListener(() => _playPausePressed = true);
            _stopReplayModeButton.onClick.AddListener(() => _stopReplayModePressed = true);

            _slider.value = 0;
            _stopReplayModeButton.gameObject.SetActive(false);
        }

        private void Update()
        {
            _slider.maxValue = _replaySystem.MaxRewind;
            
            // Pause
            if (_stopReplayModePressed)
            {
                StopReplayMode();
            }
            else if (_playPausePressed)
            {
                if (!_replayMode)
                    StartReplayMode();
                else
                    _autoPlay = !_autoPlay;
            }
            else 
            {
                // Auto Replay
                if (_autoPlay && _replaySystem.RewindCounter == (int)_slider.value 
                    && _slider.value > 0)
                {
                    // Todo make playback framerate-independent
                    _slider.value -= 1;
                    RequestRewind((int)_slider.value);
                }
                else
                {
                    // Scrub Slider
                    _autoPlay = false;
                    if (_replaySystem.RewindCounter != (int)_slider.value)
                    {
                        RequestRewind((int)_slider.value);
                        _autoPlay = false;
                    }
                }
            }

            _pausePlayButtonImage.sprite = !_replayMode || _autoPlay ? _pauseSprite : _playSprite;
            _pausePlayButton.interactable = !_replayMode || _replaySystem.RewindCounter > 0;
            _playPausePressed = false;
            _stopReplayModePressed = false;
        }

        private void StartReplayMode()
        {
            RequestRewind(0);
            _autoPlay = false;
        }

        private void StopReplayMode()
        {
            _stopReplayModeButton.gameObject.SetActive(false);
            _autoPlay = false;
            _replayMode = false;
            _replaySystem.RequestUnPause();
            _slider.value = 0;
        }

        private void RequestRewind(int rewindValue)
        {
            _replaySystem.RequestSetRewindCounter(rewindValue);
            _stopReplayModeButton.gameObject.SetActive(true);
            _replayMode = true;
        }
    }
}