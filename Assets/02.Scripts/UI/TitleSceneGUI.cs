using System;
using System.Collections.Generic;
using _02.Scripts.Manager;
using Cysharp.Threading.Tasks;
using UnityEngine;
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
        
        [Header("SFX")] 
        [SerializeField] private AudioClip buttonSoundClip;
        
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
        }

        public void AppearTitleUI()
        {
            buttonContainer.AddToClassList("appear");
            logo.AddToClassList("appear");
        }

        private async UniTask RegisterEvents()
        {
            foreach (var button in _root.Query<Button>().ToList())
            {
                button.clicked += OnClickButton;
            }

            await UniTask.WaitUntil(() => LevelManager.instance != null);
        }

        private void OnClickButton()
        {
            SoundManager.instance.PlaySfx(buttonSoundClip, volume: 10f);
        }
    }
}