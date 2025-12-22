using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using _02.Scripts.Manager;
using _02.Scripts.Utils;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace _02.Scripts.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class GameViewGUI : MonoBehaviour
    {
        private UIDocument _uiDocument;
        private VisualElement _root;

        [NonSerialized] private VisualElement _bossHpBar, _bossComboBar, _pauseWindow,
            _checkpointOutline, _clearWindow, _starContainer;

        [NonSerialized] private Label _judgementLabel, _levelNameLabel, _bossNameLabel,
            _pauseWindowCurTimeLabel, _pauseWindowAccuracyLabel, _pauseWindowProgressLabel,
            _checkpointLabel, _prepareCountdownLabel,
            _clearWindowAccuracyLabel, _clearWindowPlayTimeLabel, _checkpointUseCountLabel;

        private Button _playBtn, _goToTitleBtn, _levelEditorBtn, _clearWindowRestartBtn, _clearWindowContinueBtn;

        [Header("SFX")] 
        [SerializeField] private AudioClip buttonSoundClip;
        [SerializeField] private float buttonSoundVolume = 1f;
        [SerializeField] private AudioClip starGettingSound;
        [SerializeField] private float starGettingSoundVolume = 1f;
        [SerializeField] private AudioClip perfectClearSound;
        [SerializeField] private float perfectClearSoundVolume = 1f;
        
        [Header("GUI")]
        [SerializeField] private LoadingScreenGUI loadingScreenGUI;
        
        [Header("Star Sprites")]
        [SerializeField] private Sprite starEmptySprite, starFullSprite, starFullPerfectSprite;
        
        private readonly List<Image> _starImages = new();
        private CancellationTokenSource _starAnimationCts;
        
        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            _root = _uiDocument.rootVisualElement;

            BindElements();
            RegisterEvents().Forget();
        }

        private void BindElements()
        {
            _pauseWindow = _root.Q<VisualElement>("PauseWindow");
            _bossHpBar = _root.Q<VisualElement>("BossBarHP");
            _bossComboBar = _root.Q<VisualElement>("BossBarCombo");

            _bossNameLabel = _root.Q<Label>("BossNameLabel");
            _levelNameLabel = _root.Q<Label>("LevelNameLabel");

            _pauseWindowAccuracyLabel = _root.Q<Label>("PauseWindowAccuracyLabel");
            _pauseWindowProgressLabel = _root.Q<Label>("PauseWindowProgressLabel");
            _pauseWindowCurTimeLabel = _root.Q<Label>("PauseWindowCurTimeLabel");
            _prepareCountdownLabel = _root.Q<Label>("PrepareCountdownLabel");
            
            _clearWindow = _root.Q<VisualElement>("ClearWindow");
            _clearWindowAccuracyLabel = _root.Q<Label>("ClearWindowAccuracyLabel");
            _clearWindowPlayTimeLabel = _root.Q<Label>("ClearWindowPlayTimeLabel");
            _checkpointUseCountLabel = _root.Q<Label>("CheckpointUseCountLabel");
            _clearWindowRestartBtn = _root.Q<Button>("ClearWindowRestartBtn");
            _clearWindowContinueBtn = _root.Q<Button>("ClearWindowContinueBtn");
            
            _starContainer = _root.Q<VisualElement>("StarContainer");
            _starContainer.Query<Image>().ToList().ForEach(image => _starImages.Add(image));

            _playBtn = _root.Q<Button>("PlayBtn");
            _goToTitleBtn = _root.Q<Button>("GoToTitleBtn");
            _levelEditorBtn = _root.Q<Button>("LevelEditorBtn");
            
            _checkpointOutline = _root.Q<VisualElement>("CheckpointOutline");
            _checkpointLabel = _root.Q<Label>("CheckpointLabel");

            _judgementLabel = _root.Q<Label>("JudgementLabel");
        }

        private async UniTask RegisterEvents()
        {
            _playBtn.clicked += OnClickPlayButton;
            _goToTitleBtn.clicked += OnClickGoToTitleButton;
            _levelEditorBtn.clicked += OnClickLevelEditorButton;
            
            _clearWindowRestartBtn.clicked += RestartGame;
            _clearWindowContinueBtn.clicked += GoToStageSelectScene;
            
            foreach (var button in _root.Query<Button>().ToList())
            {
                button.clicked += OnClickButton;
            }

            await UniTask.WaitUntil(() => LevelManager.instance != null);
            LevelManager.instance.onAddJudgement += type => OnAddJudgement(type).Forget();
        }

        private void GoToStageSelectScene()
        {
            loadingScreenGUI.ShowLoadingPanel(() =>
            {
                SceneManager.LoadSceneAsync("StageSelectScene");
            });
        }

        private void RestartGame()
        {
            LevelManager.instance.LoadLevel(LevelManager.instance.currentLevel).Forget();
        }

        public void SetPrepareCountdown(int countdown)
        {
            _prepareCountdownLabel.text = countdown > 0 ? countdown.ToString() : "";
        }

        private void OnClickLevelEditorButton()
        {
            loadingScreenGUI.ShowLoadingPanel(() =>
            {
                SceneTransitionData.levelData = LevelManager.instance.currentLevel;
                SceneManager.LoadSceneAsync("LevelEditor");
            });
        }

        private void OnClickGoToTitleButton()
        {
            loadingScreenGUI.ShowLoadingPanel(() =>
            {
                SceneManager.LoadSceneAsync("TitleScene");
            });
        }

        private void OnClickPlayButton()
        {
            ClosePauseWindow();
        }

        public void OpenPauseWindow()
        {
            LevelManager.instance.Pause();
            _pauseWindow.AddToClassList("appear");
        }

        public void ClosePauseWindow()
        {
            LevelManager.instance.Play();
            _pauseWindow.RemoveFromClassList("appear");
        }

        public void TogglePauseWindow()
        {
            if (_pauseWindow.ClassListContains("appear"))
            {
                print("Close Pause Window");
                ClosePauseWindow();
            }
            else
            {
                print("Open Pause Window");
                OpenPauseWindow();
            }
        }
        
        public void OpenClearWindow()
        {
            if(_clearWindow.ClassListContains("appear")) return;
            
            _clearWindow.AddToClassList("appear");
            foreach (var starImage in _starImages)
            {
                starImage.image = starEmptySprite.texture;
            }

            StarAnimationTask().Forget();
        }

        private async UniTask StarAnimationTask()
        {
            _starAnimationCts = new CancellationTokenSource();
            try
            {
                for (var i = 0; i < LevelManager.instance.currentLevelPlayerData.GetStarCount(); i++)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: _starAnimationCts.Token);
                    _starImages[i].image = starFullSprite.texture;
                    SoundManager.instance.PlaySfx(starGettingSound, starGettingSoundVolume);
                }

                if (LevelManager.instance.currentLevelPlayerData.IsPerfectClear())
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(0.5f), cancellationToken: _starAnimationCts.Token);
                    
                    foreach (var starImage in _starImages)
                    {
                        starImage.image = starFullPerfectSprite.texture;
                    }
                    SoundManager.instance.PlaySfx(perfectClearSound, perfectClearSoundVolume);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
        
        public void CloseClearWindow()
        {
            _starAnimationCts?.Cancel();
            _starAnimationCts?.Dispose();
            _starAnimationCts = null;
            
            if(!_clearWindow.ClassListContains("appear")) return;
            _clearWindow.RemoveFromClassList("appear");
        }

        private void OnClickButton()
        {
            SoundManager.instance.PlaySfx(buttonSoundClip, volume: buttonSoundVolume);
        }

        private async UniTask OnAddJudgement(JudgementType type)
        {
            _judgementLabel.text = type.ToString().ToUpper();
            _judgementLabel.AddToClassList("appear");
            await UniTask.WaitForSeconds(0.1f);
            _judgementLabel.RemoveFromClassList("appear");
        }

        public async UniTask TriggerCheckpointVFX()
        {
            _checkpointOutline.AddToClassList("appear");
            _checkpointLabel.AddToClassList("appear");
            await UniTask.WaitForSeconds(0.1f);
            _checkpointOutline.RemoveFromClassList("appear");
            _checkpointLabel.RemoveFromClassList("appear");
        }

        private void Update()
        {
            if(!LevelManager.instance || 
               !LevelManager.instance.currentBoss || 
               LevelManager.instance.currentLevel == null) return;

            var level = LevelManager.instance.currentLevel;
            var boss = LevelManager.instance.currentBoss;
            
            _bossHpBar.style.right = new StyleLength(new Length(
                100f - (float)boss.hp / boss.maxHp * 100f, LengthUnit.Percent
                ));
            _bossComboBar.style.right = _bossHpBar.style.right;
            _bossComboBar.style.transitionDelay =
                new StyleList<TimeValue>(
                    new List<TimeValue>(
                        new[] { new TimeValue(
                            LevelManager.instance.BeatToPlayTime(0.5f) 
                            + LevelManager.instance.judgementTimeSettings.perfect * 2f
                            ) }
                        )
                    );
            _bossComboBar.style.transitionDuration =
                new StyleList<TimeValue>(
                    new List<TimeValue>(
                        new[] { new TimeValue(LevelManager.instance.BeatToPlayTime(0.25f)) }
                    )
                );

            _bossNameLabel.text = boss.bossName;
            _levelNameLabel.text = $"{level.levelName} ({level.authorName} - {level.musicName})";

            var curPlayTimeMin = Mathf.Max(0, Mathf.FloorToInt(LevelManager.instance.currentPlayTime / 60f));
            var curPlayTimeSec = Mathf.Max(0, Mathf.FloorToInt(LevelManager.instance.currentPlayTime % 60f));
            
            var maxPlayTime = LevelManager.instance.maxPlayTime;
            var maxPlayTimeMin = Mathf.Max(0, Mathf.FloorToInt(maxPlayTime / 60f));
            var maxPlayTimeSec = Mathf.Max(0, Mathf.FloorToInt(maxPlayTime % 60f));
            
            _pauseWindowAccuracyLabel.text = $"{LevelManager.instance.currentLevelPlayerData.GetAccuracy():0.00}%";
            _pauseWindowCurTimeLabel.text = $"{curPlayTimeMin:00}:{curPlayTimeSec:00} / {maxPlayTimeMin:00}:{maxPlayTimeSec:00}";

            _clearWindowAccuracyLabel.text = $"{LevelManager.instance.currentLevelPlayerData.GetAccuracy():0.00}%";
            _clearWindowPlayTimeLabel.text = $"{maxPlayTimeMin:00}:{maxPlayTimeSec:00}";
            _checkpointUseCountLabel.text = $"{LevelManager.instance.currentLevelPlayerData.respawnCount}번";

            var progress = Mathf.Clamp01(
                LevelManager.instance.currentBeat
                / (LevelManager.instance.currentLevel.pattern
                    .OrderByDescending(n => n.appearBeat)
                    .FirstOrDefault()?.appearBeat ?? 1f)) * 100f;
            _pauseWindowProgressLabel.text = $"{progress:0.00}%";
        }
    }
}