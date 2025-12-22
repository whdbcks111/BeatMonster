using System;
using _02.Scripts.Manager;
using _02.Scripts.ScriptableObjects;
using _02.Scripts.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace _02.Scripts.UI
{
    public class StageSelectGUI : MonoBehaviour
    {
        [SerializeField] private UIDocument bossNameLabelUIDoc;
        [SerializeField] private UIDocument stageNameLabelUIDoc;
        
        public SpriteRenderer stageSpriteRenderer;
        [NonSerialized] public StageData currentStageData;
        [NonSerialized] public StageSelectSceneManager stageSelectSceneManager;
        [NonSerialized] public bool isSelected = false;
        
        private VisualElement _bossNameLabelRoot, _stageNameLabelRoot;
        private Label _bossNameLabel, _stageNameLabel, _musicNameLabel;
        private Button _stageEnterBtn;

        private void OnEnable()
        {
            _bossNameLabelRoot = bossNameLabelUIDoc.rootVisualElement;
            _stageNameLabelRoot = stageNameLabelUIDoc.rootVisualElement;
            
            _bossNameLabel = _bossNameLabelRoot.Q<Label>("BossNameLabel");
            _stageNameLabel = _stageNameLabelRoot.Q<Label>("StageNameLabel");
            _musicNameLabel = _stageNameLabelRoot.Q<Label>("MusicNameLabel");
            
            _stageEnterBtn = _stageNameLabelRoot.Q<Button>("StageEnterBtn");
            _stageEnterBtn.clicked += () =>
            {
                if(currentStageData == null) return;
                
                _stageEnterBtn.SetEnabled(false);
                stageSelectSceneManager.loadingScreenGUI.ShowLoadingPanel(() =>
                {
                    SceneTransitionData.levelData = currentStageData.levelData;
                    SceneTransitionData.currentWorldIndex = stageSelectSceneManager.currentWorldIndex;
                    SceneTransitionData.currentStageIndex = stageSelectSceneManager.currentStageIndex;
                    SceneManager.LoadSceneAsync("GameScene");
                });
            };
        }
        
        private void Update()
        {
            if(currentStageData == null) return;
            
            _bossNameLabel.text = currentStageData.boss.bossName;
            _stageNameLabel.text = currentStageData.levelData.levelName;
            _musicNameLabel.text = $"{currentStageData.levelData.authorName} - {currentStageData.levelData.musicName}";
            stageSpriteRenderer.sprite = currentStageData.boss.displaySprite;
            
            _stageEnterBtn.style.display = isSelected ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}