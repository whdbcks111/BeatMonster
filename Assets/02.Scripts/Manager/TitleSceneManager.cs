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

        private float currentBeat => (float)((AudioSettings.dspTime - _startDspTime) / 60f * bgmBpm);
        private double _startDspTime;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            print(scene.name + "has loaded");
        }
        
        private void Start()
        {
            StartTask().Forget();
        }

        private void Update()
        {
            var scale = Mathf.Pow(1 - ExtraMath.PositiveMod(currentBeat + offset, 1f), 2f);

            playerDummy.transform.localScale = new Vector3(1f, Mathf.Lerp(1f, 1.1f, scale), 1f);
            gui.logo.style.scale = new StyleScale(Vector2.one * Mathf.Lerp(1f, 1.05f, scale));
            gui.buttonContainer.style.scale = new StyleScale(Vector2.one * Mathf.Lerp(1f, 1.05f, scale));
        }

        private async UniTask StartTask()
        {
            var startTime = Time.unscaledTime;
            await UniTask.WaitUntil(() => Time.deltaTime < 0.1f || Time.unscaledTime - startTime > 5f);
            await UniTask.Yield();
            _startDspTime = AudioSettings.dspTime + 0.5f;
            bgmSource.PlayScheduled(_startDspTime);
            gui.AppearTitleUI();
        }
    }
}