using System;
using System.Collections.Generic;
using System.Linq;
using _02.Scripts.Settings;
using _02.Scripts.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace _02.Scripts.Manager
{
    public class CalibrationManager : MonoBehaviour
    {
        [SerializeField] private UIDocument calibrationUIDoc;
        [SerializeField] private LoadingScreenGUI loadingScreenGUI;
        
        [SerializeField] private Sprite emptyMarkerSprite, filledMarkerSprite;
        [SerializeField] private AudioSource audioSource;
        
        [SerializeField] private AudioClip beatClip;
        [SerializeField] private float beatVolume;
        
        [SerializeField] private float bpm = 120f;
        
        private VisualElement _markerContainer;
        private readonly List<Image> _markers = new();
        private readonly List<float> _offsets = new();
        private Label _offsetLabel;
        private Image _exitBtn;

        private bool _isFinished = false;
        
        private double _nextDspTime = double.NaN;

        private void OnEnable()
        {
            var root = calibrationUIDoc.rootVisualElement;
            _markerContainer = root.Q<VisualElement>("MarkerContainer");

            _markerContainer.Query<Image>().ForEach(img =>
            {
                img.image = emptyMarkerSprite.texture;
                _markers.Add(img);
            });
            
            _offsetLabel = root.Q<Label>("OffsetLabel");
            _exitBtn = root.Q<Image>("ExitBtn");
            
            _exitBtn.RegisterCallback<ClickEvent>(e =>
            {
                loadingScreenGUI.ShowLoadingPanel(() =>
                {
                    SceneManager.LoadSceneAsync("TitleScene");
                });
            });
        }

        private void Start()
        {
            
            loadingScreenGUI.HideLoadingPanel(() =>
            {
                _nextDspTime = AudioSettings.dspTime + 0.5f;
                audioSource.PlayScheduled(_nextDspTime);
            });
        }

        private void Update()
        {
            if(double.IsNaN(_nextDspTime)) return;
            
            if (AudioSettings.dspTime >= _nextDspTime - 1 / bpm * 60)
            {
                _nextDspTime += 1 / bpm * 60;
                SoundManager.instance.PlaySfxScheduled(beatClip, _nextDspTime, beatVolume);
            }

            if (Keyboard.current.anyKey.wasPressedThisFrame && _offsets.Count < _markers.Count)
            {
                _markers[_offsets.Count].image = filledMarkerSprite.texture;
                var offset = Mathf.Min(
                    Mathf.Abs((float)(_nextDspTime - AudioSettings.dspTime)),
                    Mathf.Abs((float)((_nextDspTime - 1 / bpm * 60) - AudioSettings.dspTime)),
                    Mathf.Abs((float)((_nextDspTime - 2 / bpm * 60) - AudioSettings.dspTime)),
                    Mathf.Abs((float)((_nextDspTime + 1 / bpm * 60) - AudioSettings.dspTime)),
                    Mathf.Abs((float)((_nextDspTime + 2 / bpm * 60) - AudioSettings.dspTime))
                );
                _offsets.Add(offset);
            }

            if (_offsets.Count > 0)
            {
                _offsetLabel.text = $"{Mathf.RoundToInt(_offsets.Average() * 1000)}";
            }

            if (_offsets.Count >= _markers.Count && !_isFinished)
            {
                _isFinished = true;
                OnFinish().Forget();
            }
        }

        private async UniTask OnFinish()
        {
            var settings = GameSettings.settings;
            settings.audioOffset = _offsets.Average();
            GameSettings.settings = settings;
            
            await UniTask.Delay(TimeSpan.FromSeconds(1f));
            await loadingScreenGUI.ShowLoadingPanelAsync();
            await SceneManager.LoadSceneAsync("TitleScene");
        }
    }
}