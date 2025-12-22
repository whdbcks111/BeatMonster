using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace _02.Scripts.UI
{
    public class LoadingScreenGUI : MonoBehaviour
    {
        [SerializeField] private float loadingTextTime = 0.3f;
        [SerializeField] private float minLoadingTime = 0.5f;
        [SerializeField] private float loadingPanelFadeTime = 0.2f;
        [SerializeField] private bool showOnAwake = true;
        
        private UIDocument _uiDocument;
        private VisualElement _root;
        
        private VisualElement _loadingPanel;
        private Label _loadingLabel;
        private Image _loadingImage;

        private float _dotTimer;
        private int _dotCount;
        
        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            _root = _uiDocument.rootVisualElement;

            BindElements();
        }

        private void BindElements()
        {
            _loadingPanel = _root.Q<VisualElement>("LoadingPanel");
            _loadingLabel = _root.Q<Label>("LoadingLabel");
            _loadingImage = _root.Q<Image>("LoadingImage");
            
            _loadingPanel.style.display = showOnAwake ? DisplayStyle.Flex : DisplayStyle.None;
            _loadingPanel.style.opacity = showOnAwake ? 1f : 0f;
        }

        private void Update()
        {
            _dotTimer += Time.unscaledDeltaTime;
            if (_dotTimer >= loadingTextTime)
            {
                _dotTimer = 0f;
                _dotCount = _dotCount % 3 + 1;
                _loadingLabel.text = "Loading" + new string('.', _dotCount);
            }

            _loadingImage.style.scale = new Vector2(1f, 
                1f + (1f + Mathf.Sin((Time.unscaledTime % loadingTextTime) / loadingTextTime * Mathf.PI * 2f)) / 2f * 0.3f);
        }
        
        public void ShowLoadingPanel(Action onComplete = null)
        {
            if (_loadingPanel.style.display == DisplayStyle.Flex) return;
            
            ShowLoadingPanelAsync().ContinueWith(() => onComplete?.Invoke()).Forget();
        }

        public async UniTask ShowLoadingPanelAsync()
        {
            if (_loadingPanel.style.opacity.value > 0f) return;
            _loadingPanel.style.display = DisplayStyle.Flex;
            
            for(var t = 0f; t <= 1f; t += Time.unscaledDeltaTime / loadingPanelFadeTime) {
                _loadingPanel.style.opacity = t;
                await UniTask.Yield();
            }
            _loadingPanel.style.opacity = 1f;
        }
        
        public void HideLoadingPanel(Action onComplete = null)
        {
            if (_loadingPanel.style.opacity.value < 1f) return;
            
            HideLoadingPanelAsync().ContinueWith(() => onComplete?.Invoke()).Forget();
        }
        
        public async UniTask HideLoadingPanelAsync()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(minLoadingTime));
            
            for(var t = 0f; t <= 1f; t += Time.unscaledDeltaTime / loadingPanelFadeTime)
            {
                _loadingPanel.style.opacity = 1f - t;
                await UniTask.Yield();
            }
            _loadingPanel.style.opacity = 0f;
            
            _loadingPanel.style.display = DisplayStyle.None;
        }
    }
}