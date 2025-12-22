using System;
using System.Collections.Generic;
using _02.Scripts.ScriptableObjects;
using _02.Scripts.UI;
using _02.Scripts.Utils;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace _02.Scripts.Manager
{
    public class StageSelectSceneManager : MonoBehaviour
    {
        public WorldData[] worlds;
        public LoadingScreenGUI loadingScreenGUI;

        [SerializeField] private Transform playerDummyTransform;
        [SerializeField] private StageSelectGUI stageSelectGUIPrefab;
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private UIDocument stageSelectSceneUIDoc;
        
        [NonSerialized] public int currentWorldIndex = 0;
        [NonSerialized] public int currentStageIndex = 0;
        
        [Header("SFX")] 
        [SerializeField] private AudioClip buttonSoundClip;
        [SerializeField] private float buttonSoundVolume = 1f;

        private float _lerpStageIndex = 0f;

        private double _musicStartDspTime = -1;
        
        private readonly List<StageSelectGUI> _stageSelectGuiList = new();
        
        private void Start()
        {
            if (SceneTransitionData.currentWorldIndex >= 0)
            {
                currentWorldIndex = Mathf.Clamp(SceneTransitionData.currentWorldIndex, 0, worlds.Length - 1);
            }

            if (SceneTransitionData.currentStageIndex >= 0)
            {
                currentStageIndex = Mathf.Clamp(SceneTransitionData.currentStageIndex, 0, worlds[currentWorldIndex].stages.Length - 1);
            }
            
            _lerpStageIndex = currentStageIndex;
            
            
            loadingScreenGUI.HideLoadingPanel(() =>
            {
                PlayStageBGM().Forget();
            });
        }

        private void OnEnable()
        {
            var root = stageSelectSceneUIDoc.rootVisualElement;
            root.Q<Image>("PrevStageBtn").RegisterCallback<ClickEvent>(evt => GoToPrevStage());
            root.Q<Image>("NextStageBtn").RegisterCallback<ClickEvent>(evt => GoToNextStage());
            root.Q<Image>("GoToTitleBtn").RegisterCallback<ClickEvent>(evt => GoToTitleScene());
            
            foreach (var button in root.Query<Button>().ToList())
            {
                button.clicked += OnClickButton;
            }
        }

        private void OnClickButton()
        {
            SoundManager.instance.PlaySfx(buttonSoundClip, volume: buttonSoundVolume);
        }

        private void Update()
        {
            currentWorldIndex = Mathf.Clamp(currentWorldIndex, 0, worlds.Length - 1);
            currentStageIndex = Mathf.Clamp(currentStageIndex, 0, worlds[currentWorldIndex].stages.Length - 1);
            
            var world = worlds[currentWorldIndex];
            var stage = world.stages[currentStageIndex];

            while (_stageSelectGuiList.Count < world.stages.Length)
            {
                var instance = Instantiate(stageSelectGUIPrefab, transform);
                instance.stageSelectSceneManager = this;
                _stageSelectGuiList.Add(instance);
            }

            while (_stageSelectGuiList.Count > world.stages.Length)
            {
                Destroy(_stageSelectGuiList[^1].gameObject);
                _stageSelectGuiList.RemoveAt(_stageSelectGuiList.Count - 1);
            }
            
            _lerpStageIndex = Mathf.Lerp(_lerpStageIndex, currentStageIndex, Mathf.Clamp(Time.deltaTime * 10f, 0.1f, 1f));
            
            for (var i = 0; i < _stageSelectGuiList.Count; i++)
            {
                var stageSelectGui = _stageSelectGuiList[i];
                stageSelectGui.isSelected = i == currentStageIndex;
                stageSelectGui.currentStageData = world.stages[i];

                stageSelectGui.transform.position = new Vector2(
                    playerDummyTransform.position.x + 10f + (i - _lerpStageIndex) * 20f,
                    playerDummyTransform.position.y
                    );
                stageSelectGui.stageSpriteRenderer.color = i == currentStageIndex ? Color.white : Color.gray;
            }

            if (_musicStartDspTime >= 0)
            {
                var curMusicTime = AudioSettings.dspTime - _musicStartDspTime + 0.25;
                var bpm = stage.levelData?.defaultBpm ?? 120f;
                var curBeat = (float)curMusicTime * bpm / 60f;
                var m = ExtraMath.PositiveMod(curBeat, 1f);
                
                playerDummyTransform.localScale = Mathf.Lerp(1f, 1.1f, Mathf.Pow(1 - m, 2f)) * Vector3.one;
                foreach (var stageSelectGUI in _stageSelectGuiList)
                {
                    stageSelectGUI.stageSpriteRenderer.transform.localScale = playerDummyTransform.localScale;
                }
            }
        }

        public void GoToNextStage()
        {
            currentStageIndex++;
            if (currentStageIndex >= worlds[currentWorldIndex].stages.Length)
            {
                currentWorldIndex++;
                if (currentWorldIndex >= worlds.Length) currentWorldIndex = 0;
                currentStageIndex = 0;
            }
            else if (currentStageIndex < 0)
            {
                currentWorldIndex--;
                if (currentWorldIndex < 0) currentWorldIndex = worlds.Length - 1;
                currentStageIndex = worlds[currentWorldIndex].stages.Length - 1;
            }
            
            PlayStageBGM().Forget();
        }
        
        public void GoToPrevStage()
        {
            currentStageIndex--;
            if (currentStageIndex >= worlds[currentWorldIndex].stages.Length)
            {
                currentWorldIndex++;
                if (currentWorldIndex >= worlds.Length) currentWorldIndex = 0;
                currentStageIndex = 0;
            }
            else if (currentStageIndex < 0)
            {
                currentWorldIndex--;
                if (currentWorldIndex < 0) currentWorldIndex = worlds.Length - 1;
                currentStageIndex = worlds[currentWorldIndex].stages.Length - 1;
            }
            
            PlayStageBGM().Forget();
        }

        public void GoToTitleScene()
        {
            loadingScreenGUI.ShowLoadingPanel(() =>
            {
                SceneManager.LoadSceneAsync("TitleScene");
            });
        }

        private async UniTask PlayStageBGM()
        {
            var levelData = worlds[currentWorldIndex].stages[currentStageIndex].levelData;
            bgmSource.clip = await LevelManager.GetAudioClipFromLevel(levelData);
            bgmSource.Play();
            _musicStartDspTime = AudioSettings.dspTime;
        }
    }
}