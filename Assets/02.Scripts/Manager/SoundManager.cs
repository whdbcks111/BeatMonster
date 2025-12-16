using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Pool;

namespace _02.Scripts.Manager
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager instance { get; private set; }
        
        public AudioMixerGroup sfxGroup;

        private ObjectPool<AudioSource> _pool;

        private void Awake()
        {
            instance = this;
            
            _pool = new ObjectPool<AudioSource>(
                () => {
                    var source = gameObject.AddComponent<AudioSource>();
                    source.outputAudioMixerGroup = sfxGroup;
                    source.playOnAwake = false;
                    return source;
                },
                source => source.enabled = true,
                source => source.enabled = false,
                Destroy);
            
            _pool.Release(_pool.Get());
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public void PlaySfx(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            var source = _pool.Get();
            source.volume = volume;
            source.pitch = pitch;
            source.clip = clip;
            source.Play();
            WaitForReturn(source).Forget();
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public void PlaySfxScheduled(AudioClip clip, double dspTime, float volume = 1f, float pitch = 1f)
        {
            var source = _pool.Get();
            source.volume = volume;
            source.pitch = pitch;
            source.clip = clip;
            source.PlayScheduled(dspTime);
            WaitForReturn(source).Forget();
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private async UniTask WaitForReturn(AudioSource source)
        {
            if(source.clip) await UniTask.Delay(TimeSpan.FromSeconds(source.clip.length + 1f), cancellationToken: destroyCancellationToken);
            if(source) _pool.Release(source);
        }
    }
}