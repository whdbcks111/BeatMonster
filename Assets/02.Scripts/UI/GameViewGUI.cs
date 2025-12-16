using System;
using System.Collections.Generic;
using _02.Scripts.Manager;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace _02.Scripts.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class GameViewGUI : MonoBehaviour
    {
        private UIDocument _uiDocument;
        private VisualElement _root;

        [NonSerialized] public VisualElement bossHpBar, bossComboBar, pauseWindow;

        [NonSerialized] public Label judgementLabel, levelNameLabel, bossNameLabel,
            pauseWindowCurTimeLabel, pauseWindowAccuracyLabel, pauseWindowProgressLabel;

        [NonSerialized] public Button playBtn, goToTitleBtn;
        
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
            pauseWindow = _root.Q<VisualElement>("PauseWindow");
            bossHpBar = _root.Q<VisualElement>("BossBarHP");
            bossComboBar = _root.Q<VisualElement>("BossBarCombo");

            bossNameLabel = _root.Q<Label>("BossNameLabel");
            levelNameLabel = _root.Q<Label>("LevelNameLabel");

            pauseWindowAccuracyLabel = _root.Q<Label>("PauseWindowAccuracyLabel");
            pauseWindowProgressLabel = _root.Q<Label>("PauseWindowProgressLabel");
            pauseWindowCurTimeLabel = _root.Q<Label>("PauseWindowCurTimeLabel");

            playBtn = _root.Q<Button>("PlayBtn");
            goToTitleBtn = _root.Q<Button>("GoToTitleBtn");

            judgementLabel = _root.Q<Label>("JudgementLabel");
        }

        private async UniTask RegisterEvents()
        {
            playBtn.clicked += OnClickPlayButton;
            goToTitleBtn.clicked += OnClickGoToTitleButton;
            
            foreach (var button in _root.Query<Button>().ToList())
            {
                button.clicked += OnClickButton;
            }

            await UniTask.WaitUntil(() => LevelManager.instance != null);
            LevelManager.instance.onAddJudgement += type => OnAddJudgement(type).Forget();
        }

        private void OnClickGoToTitleButton()
        {
            throw new NotImplementedException();
        }

        private void OnClickPlayButton()
        {
            ClosePauseWindow();
        }

        public void OpenPauseWindow()
        {
            LevelManager.instance.Pause();
            pauseWindow.style.display = DisplayStyle.Flex;
        }

        public void ClosePauseWindow()
        {
            LevelManager.instance.Play();
            pauseWindow.style.display = DisplayStyle.None;
        }

        private void OnClickButton()
        {
            SoundManager.instance.PlaySfx(buttonSoundClip, volume: 10f);
        }

        private async UniTask OnAddJudgement(JudgementType type)
        {
            judgementLabel.text = type.ToString().ToUpper();
            judgementLabel.AddToClassList("appear");
            await UniTask.WaitForSeconds(0.1f);
            judgementLabel.RemoveFromClassList("appear");
        }

        private void Update()
        {
            if(!LevelManager.instance || 
               !LevelManager.instance.currentBoss || 
               LevelManager.instance.currentLevel == null) return;

            var level = LevelManager.instance.currentLevel;
            var boss = LevelManager.instance.currentBoss;
            
            bossHpBar.style.right = new StyleLength(new Length(
                100f - (float)boss.hp / boss.maxHp * 100f, LengthUnit.Percent
                ));
            bossComboBar.style.right = bossHpBar.style.right;
            bossComboBar.style.transitionDelay =
                new StyleList<TimeValue>(
                    new List<TimeValue>(
                        new[] { new TimeValue(
                            LevelManager.instance.BeatToPlayTime(0.5f) 
                            + LevelManager.instance.judgementTimeSettings.perfect * 2f
                            ) }
                        )
                    );
            bossComboBar.style.transitionDuration =
                new StyleList<TimeValue>(
                    new List<TimeValue>(
                        new[] { new TimeValue(LevelManager.instance.BeatToPlayTime(0.25f)) }
                    )
                );

            bossNameLabel.text = boss.bossName;
            levelNameLabel.text = $"{level.levelName} ({level.authorName} - {level.musicName})";
        }
    }
}