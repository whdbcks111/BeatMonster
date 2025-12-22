using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly List<AudioSource> _activeSources = new();

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
                source =>
                {
                    source.enabled = true;
                    _activeSources.Add(source);
                },
                source =>
                {
                    source.Stop();
                    source.enabled = false;
                    _activeSources.Remove(source);
                },
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
            WaitForRelease(source).Forget();
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public void PlaySfxScheduled(AudioClip clip, double dspTime, float volume = 1f, float pitch = 1f)
        {
            var source = _pool.Get();
            source.volume = volume;
            source.pitch = pitch;
            source.clip = clip;
            source.PlayScheduled(dspTime);
            WaitForReleaseScheduled(source, dspTime).Forget();
        }

        public void StopAllSfx()
        {
            var releaseTarget = _activeSources.Where(activeSource => activeSource.enabled).ToList();
            foreach (var audioSource in releaseTarget)
            {
                _pool.Release(audioSource);
            }
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private async UniTask WaitForRelease(AudioSource source)
        {
            if (source.clip)
            {
                await UniTask.WaitWhile(() => source.enabled && source.isPlaying,
                    cancellationToken: source.GetCancellationTokenOnDestroy());
                if(source && source.enabled) _pool.Release(source);
            }
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private async UniTask WaitForReleaseScheduled(AudioSource source, double dspTime)
        {
            if (source.clip)
            {
                await UniTask.WaitWhile(() => source.enabled && AudioSettings.dspTime <= dspTime,
                    cancellationToken: source.GetCancellationTokenOnDestroy());
                await UniTask.WaitWhile(() => source.enabled && source.isPlaying,
                    cancellationToken: source.GetCancellationTokenOnDestroy());
                if(source && source.enabled) _pool.Release(source);
            }
        }
    }
}