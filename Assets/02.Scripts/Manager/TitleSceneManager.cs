using System;
using _02.Scripts.UI;
using _02.Scripts.Utils;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace _02.Scripts.Manager
{
    public class TitleSceneManager : MonoBehaviour
    {
        [SerializeField] private float offset = 0.05f;
        [SerializeField] private float bgmBpm;
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private SpriteRenderer playerDummy;
        [SerializeField] private TitleSceneGUI gui;
        [SerializeField] private LoadingScreenGUI loadingScreenGUI;

        private Vector3 _originalPlayerScale;

        private float currentBeat => (float)((AudioSettings.dspTime - _startDspTime) / 60f * bgmBpm);
        private double _startDspTime;
        
        private void Start()
        {
            StartTask().Forget();
            
            _originalPlayerScale = playerDummy.transform.localScale;
        }

        private void Update()
        {
            var scale = Mathf.Pow(1 - ExtraMath.PositiveMod(currentBeat + offset, 1f), 2f);

            playerDummy.transform.localScale = _originalPlayerScale + Vector3.up * Mathf.Lerp(0f, 0.2f, scale);
            gui.logo.style.scale = new StyleScale(Vector2.one * Mathf.Lerp(1f, 1.05f, scale));
            gui.buttonContainer.style.scale = new StyleScale(Vector2.one * Mathf.Lerp(1f, 1.05f, scale));
        }

        private async UniTask StartTask()
        {
            var startTime = Time.unscaledTime;
            await UniTask.WaitUntil(() => Time.deltaTime < 0.1f || Time.unscaledTime - startTime > 5f);
            await UniTask.Yield();
            await loadingScreenGUI.HideLoadingPanelAsync();
            gui.AppearTitleUI();
            bgmSource.PlayDelayed(0.5f);
        }
    }
}