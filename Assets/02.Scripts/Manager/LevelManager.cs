using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _02.Scripts.Level;
using _02.Scripts.Level.Note;
using _02.Scripts.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;

namespace _02.Scripts.Manager
{
    [RequireComponent(typeof(AudioSource))]
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager instance { get; private set; }

        public double dspTime => _interpolatedDspTime;
        public bool isPlaying { get; private set; } = false;
        public float currentPlayTime => (float)(_accDspTime + (
            isPlaying ? 
                dspTime - _startDspTime : 
                0.0
            ));

        public float currentBeat => currentLevel == null ? -1f : currentPlayTime * currentLevel.defaultBpm / 60f;

        public float offset = 0.24f;
        public Player player;
        public LevelPlayerData currentLevelPlayerData;
        [NonSerialized] public Level currentLevel;
        [NonSerialized] public Boss currentBoss;
        [NonSerialized] public readonly List<NoteObject> noteObjects = new();
        [NonSerialized] public bool isLoaded = false;

        [Header("Gameplay Settings")] 
        public JudgementTime judgementTime;
        public bool playAtAwake = false;
        
        [Header("Note Hit-Point Prefabs")]
        public GameObject attackPoint;
        public GameObject defendPoint;

        private double _startDspTime = 0, _pauseDspTime = 0, _accDspTime = -2.0;
        private AudioSource _audioSource;
        private float _checkpointEnterTime = float.NaN;
        private Camera _camera;

        private double _prevDspTime, _interpolatedDspTime;

        [Header("Checkpoint Shader Control")]
        [SerializeField] private Material checkpointMat;
        
        [Header("Background Objects")]
        [SerializeField] private SpriteRenderer groundObject;
        [SerializeField] private SpriteRenderer skyObject;
        
        private static readonly int CheckpointVfxRadius = Shader.PropertyToID("_Radius");
        private static readonly int CheckpointVfxCenter = Shader.PropertyToID("_Center");

        private void Awake()
        {
            instance = this;

            _camera = Camera.main;
            _prevDspTime = _interpolatedDspTime = dspTime;
            _audioSource = GetComponent<AudioSource>();

            if (!_camera) return;
            
            checkpointMat.SetFloat(CheckpointVfxRadius, 0f);
            checkpointMat.SetVector(CheckpointVfxCenter, _camera.WorldToScreenPoint(player.bodyCenter.position));
                
            groundObject.transform.position = new Vector3(
                _camera.ViewportToWorldPoint(new Vector3(.5f, .5f)).x, 
                groundObject.transform.position.y);
            groundObject.size = _camera.ViewportToWorldPoint(new Vector3(2f, 1)) -
                                _camera.ViewportToWorldPoint(new Vector3(-1f, 0));
            skyObject.transform.position = new Vector3(
                _camera.ViewportToWorldPoint(new Vector3(.5f, .5f)).x, 
                skyObject.transform.position.y);
            skyObject.size = _camera.ViewportToWorldPoint(new Vector3(1.1f, 1)) -
                             _camera.ViewportToWorldPoint(new Vector3(-0.1f, 0));
        }

        private void Start()
        {
            LoadLevel().Forget();
        }

        private void Update()
        {
            InterpolateDspTime();
            UpdateCheckpointVfx();
            
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Seek(currentPlayTime - 5);
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                Seek(currentPlayTime + 5);
            }

            if (Input.GetKeyDown(KeyCode.C)) _checkpointEnterTime = Time.unscaledTime;
        }

        private void UpdateCheckpointVfx()
        {
            if (_checkpointEnterTime >= 0f && _camera)
            {
                checkpointMat.SetFloat(CheckpointVfxRadius,
                    Mathf.Log(Time.unscaledTime - _checkpointEnterTime + 1f) * _camera.pixelWidth);
            }
        }

        private void InterpolateDspTime()
        {
            if (Math.Abs(_prevDspTime - AudioSettings.dspTime) < 0.00001)
            {
                _interpolatedDspTime += Time.unscaledDeltaTime;
            }
            else if (_interpolatedDspTime < AudioSettings.dspTime)
            {
                _prevDspTime = _interpolatedDspTime = AudioSettings.dspTime;
            }
        }

        private async UniTask LoadLevel()
        {
            currentLevel = JsonUtility.FromJson<Level>(await File.ReadAllTextAsync(
                Path.Combine(Application.dataPath, "Test.json")
                ));
            
            var bossObj = await Addressables.InstantiateAsync($"Boss/{currentLevel.bossId}");
            if (!bossObj || !bossObj.TryGetComponent(out currentBoss))
            {
                throw new Exception($"Boss Load Failed (Id: {currentLevel.bossId})");
            }

            _audioSource.clip = await Addressables.LoadAssetAsync<AudioClip>($"Music/{currentLevel.musicId}");
            if (!_audioSource.clip)
            {
                throw new Exception($"Music Load Failed (Id: {currentLevel.musicId})");
            }

            InitGame();
            
            if (playAtAwake)
            {
                _startDspTime = dspTime + offset + currentLevel.startOffset;
                _pauseDspTime = _startDspTime - 1;
                
                _audioSource.PlayScheduled(_startDspTime - offset - currentLevel.startOffset);
                
                isPlaying = true;
            }
            isLoaded = true;
        }

        private void InitGame()
        {
            Pause();
            _audioSource.Stop();
            var parryCount = 0;

            foreach (var noteObj in noteObjects)
            {
                Destroy(noteObj.gameObject);
            }
            noteObjects.Clear();
            
            foreach (var note in currentLevel.pattern)
            {
                var prefab = currentBoss.noteMap[note.noteType];
                var noteObj = Instantiate(prefab, transform, true);
                
                noteObjects.Add(noteObj);
                noteObj.note = note;
                
                if (noteObj.hitType == HitType.Attack) parryCount++;
            }
            
            currentBoss.InitHp(parryCount);

            _accDspTime = -2.0;
            _pauseDspTime = 0;
            _startDspTime = 0;
        }

        public void Pause()
        {
            if (!isPlaying) return;
            
            _pauseDspTime = dspTime;
            _accDspTime += _pauseDspTime - _startDspTime;
            if(_accDspTime >= 0f) _audioSource.Pause();
            else _audioSource.Stop();

            isPlaying = false;
        }

        public void Stop()
        {
            InitGame();
        }

        public void Seek(double time)
        {
            var isCorrectTime = 0 <= time && time < _audioSource.clip.length;
            
            _startDspTime = dspTime + offset + currentLevel.startOffset;
            _accDspTime = time;
            
            _audioSource.Stop();

            if(isCorrectTime) _audioSource.time = (float)time;
            
            if (isPlaying && time < _audioSource.clip.length)
            {
                var realStartTime = _startDspTime - offset - currentLevel.startOffset + (time < 0f ? -time : 0f);

                _audioSource.PlayScheduled(realStartTime);
            }

            var bossHp = 0;
            foreach (var noteObj in noteObjects)
            {
                noteObj.wasHit = noteObj.note.appearBeat <= currentBeat;
                noteObj.canPlayHitSound = !noteObj.wasHit;
                if (noteObj.canPlayHitSound && noteObj.hitType == HitType.Attack) ++bossHp;
            }

            currentBoss.hp = bossHp;
        }

        public NoteObject GetNextNote()
        {
            return noteObjects.Find(
                noteObj => noteObj.note.appearBeat > LevelManager.instance.currentBeat - 1f && !noteObj.wasHit
            );
        }

        public void Play()
        {
            _startDspTime = dspTime;
            if (_accDspTime < 0f)
            {
                _audioSource.PlayScheduled(dspTime - _accDspTime - offset - currentLevel.startOffset);
            }
            else if(_audioSource.isPlaying)
            {
                _audioSource.UnPause();
            }
            else
            {
                _audioSource.Play();
            }
            
            isPlaying = true;
        }

        public float BeatToPlayTime(float beat)
        {
            return beat * 60f / currentLevel.defaultBpm;
        }
    }

    [Serializable]
    public class LevelPlayerData
    {
        public int respawnCount = 0;
        public int perfectCount = 0;
        public int goodCount = 0;
        public int lateCount = 0;
        public int earlyCount = 0;
        public int missCount = 0;

        public float GetAccuracy()
        {
            var score = perfectCount + goodCount * 0.8f + (lateCount + earlyCount) * 0.5f;
            var maxScore = perfectCount + goodCount + lateCount + earlyCount + missCount + respawnCount * 2;

            if (Mathf.Approximately(maxScore, 0f)) return 0f;
            
            return score / maxScore;
        }

        public void AddJudgement(float inputTime, float originTime, JudgementTime judgementTime)
        {
            var pos = Vector3.up * 1.4f;
            var offset = inputTime - originTime;
            var absOffset = Mathf.Abs(offset);

            if (absOffset <= judgementTime.perfect)
            {
                perfectCount++;
            }
            else if (absOffset <= judgementTime.good)
            {
                goodCount++;
            }
            else if (absOffset <= judgementTime.bad)
            {
                if (offset < 0f) earlyCount++;
                else lateCount++;
            }
            else
            {
                missCount++;
            }
        } 
    }

    [Serializable]
    public class Level
    {
        public float startOffset;
        public int beatsPerMeasure;
        public int defaultBpm;
        public float baseScrollSpeed;
        public string bossId;
        public string musicId;
        
        public List<Note> pattern;
    }

    [Serializable]
    public class Note
    {
        public float appearBeat;
        public string noteType;
        public bool? isCheckpoint;
    }

    public struct JudgementTime
    {
        public float miss;
        public float perfect;
        public float good;
        public float bad;
    }
}