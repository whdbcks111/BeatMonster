using System;
using System.Collections.Generic;
using _02.Scripts.Manager;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace _02.Scripts.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class TitleSceneGUI : MonoBehaviour
    {
        private UIDocument _uiDocument;
        private VisualElement _root;

        [NonSerialized] public VisualElement buttonContainer;
        [NonSerialized] public Image logo;
        
        [NonSerialized] public Button startButton;
        [NonSerialized] public Button exitButton;
        [NonSerialized] public Button levelEditorButton;
        [NonSerialized] public Button settingsButton;
        [NonSerialized] public Button calibrationButton;
        
        [SerializeField] private LoadingScreenGUI loadingScreenGUI;
        
        [Header("SFX")] 
        [SerializeField] private AudioClip buttonSoundClip;
        [SerializeField] private float buttonSoundVolume = 1f;
        
        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            _root = _uiDocument.rootVisualElement;

            BindElements();
            RegisterEvents().Forget();
        }

        private void BindElements()
        {
            buttonContainer = _root.Q<VisualElement>("Buttons");
            logo = _root.Q<Image>("Logo");
            
            startButton = _root.Q<Button>("GameStartBtn");
            exitButton = _root.Q<Button>("ExitBtn");
            levelEditorButton = _root.Q<Button>("LevelEditorBtn");
            settingsButton = _root.Q<Button>("SettingsBtn");
            calibrationButton = _root.Q<Button>("CalibrationBtn");
        }

        public void AppearTitleUI()
        {
            buttonContainer.AddToClassList("appear");
            logo.AddToClassList("appear");
        }

        private async UniTask RegisterEvents()
        {
            startButton.clicked += StartGame;
            levelEditorButton.clicked += OnClickLevelEditorButton;
            exitButton.clicked += ExitGame;
            calibrationButton.clicked += GoToCalibrationScene;
            
            foreach (var button in _root.Query<Button>().ToList())
            {
                button.clicked += OnClickButton;
            }

            await UniTask.WaitUntil(() => LevelManager.instance != null);
        }

        private void GoToCalibrationScene()
        {
            loadingScreenGUI.ShowLoadingPanel(() =>
            {
                SceneManager.LoadScene("CalibrationScene");
            });
        }

        private void StartGame()
        {
            loadingScreenGUI.ShowLoadingPanel(() =>
            {
                SceneManager.LoadScene("StageSelectScene");
            });
        }

        private void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnClickLevelEditorButton()
        {
            loadingScreenGUI.ShowLoadingPanel(() =>
            {
                SceneManager.LoadScene("LevelEditor");
            });
        }

        private void OnClickButton()
        {
            SoundManager.instance.PlaySfx(buttonSoundClip, volume: buttonSoundVolume);
        }
    }
}