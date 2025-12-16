using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _02.Scripts.Level;
using _02.Scripts.Level.Note;
using _02.Scripts.Utils;
using _09.ScriptableObjects;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
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

        [NonSerialized] public Action onLoaded;
        [NonSerialized] public Action<JudgementType> onAddJudgement;

        public LevelEditorBindings editorBindings;

        [Header("Gameplay Settings")] 
        public JudgementTimeSettings judgementTimeSettings;
        public bool playAtLoaded = false;

        [Header("Camera Settings")] 
        public Vector2 playingViewportStart = Vector2.zero;
        public Vector2 playingViewportEnd = Vector2.one; 
        
        [Header("Note Hit-Point Prefabs")]
        public GameObject attackPoint;
        public GameObject defendPoint;

        private double _startDspTime = 0, _pauseDspTime = 0, _accDspTime = -2.0;
        private AudioSource _audioSource;
        private float _checkpointEnterTime = float.NaN;
        private float _lastCheckpointBeat = float.NaN;
        private Camera _camera;

        private double _prevDspTime, _interpolatedDspTime;

        [Header("Checkpoint Shader Control")]
        [SerializeField] private Material checkpointMat;
        
        [Header("Background Objects")]
        [SerializeField] private SpriteRenderer groundObject;
        [SerializeField] private SpriteRenderer backgroundObject;
        
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
            backgroundObject.transform.position = new Vector3(
                _camera.ViewportToWorldPoint(new Vector3(.5f, .5f)).x, 
                backgroundObject.transform.position.y);
            backgroundObject.size = _camera.ViewportToWorldPoint(new Vector3(1.1f, 1)) -
                             _camera.ViewportToWorldPoint(new Vector3(-0.1f, 0));
        }

        private void Start()
        {
            LoadLevel("$Test").Forget();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.magenta;
            if (_camera == null) _camera = Camera.main;

            if (_camera != null)
            {
                Gizmos.DrawWireCube(_camera.ViewportToWorldPoint(playingViewportStart * 0.5f + playingViewportEnd * 0.5f), 
                    _camera.ViewportToWorldPoint(playingViewportEnd) - _camera.ViewportToWorldPoint(playingViewportStart));
            }
        }

        private void Update()
        {
            InterpolateDspTime();
            UpdateCheckpointVfx();
            AdjustBossPosition();
            AdjustPlayerPosition();
        }

        public void SetCheckpoint()
        {
            _lastCheckpointBeat = currentPlayTime;
            _checkpointEnterTime = Time.unscaledTime;
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

        public async UniTask SaveLevel(string path)
        {
            if(string.IsNullOrEmpty(path)) return;
            
            var json = JsonUtility.ToJson(currentLevel, true);
            if (path.StartsWith("$"))
            {
                path = Path.Combine(Application.dataPath, "Resources/" + path[1..] + ".json");
            }
            
            await File.WriteAllTextAsync(path, json);
            Debug.Log($"Save level [{currentLevel.levelName}] to {path}");
        }

        public async UniTask LoadLevel(string path)
        {
            var level = path.StartsWith("$") ? 
                JsonUtility.FromJson<Level>(Resources.Load<TextAsset>(path[1..]).text) : 
                JsonUtility.FromJson<Level>(await File.ReadAllTextAsync(path));
            level.loadedPath = path;
            await LoadLevel(level);
            Debug.Log($"Load level [{currentLevel.levelName}] from {path} / music id: {currentLevel.musicPath}");
        }

        public async UniTask LoadLevel(Level level)
        {
            currentLevel = level;
            if (editorBindings) editorBindings.level = currentLevel;
            
            var bossObj = await Addressables.InstantiateAsync($"Boss/{currentLevel.bossId}");
            if (!bossObj || !bossObj.TryGetComponent(out currentBoss))
            {
                throw new Exception($"Boss Load Failed (Id: {currentLevel.bossId})");
            }

            if (currentLevel.musicPath.StartsWith("$"))
            {
                // music path is addressable asset key
                _audioSource.clip = await Addressables.LoadAssetAsync<AudioClip>($"Music/{currentLevel.musicPath[1..]}");
            }
            else
            {
                // music path is relative local audio file path
                
                var audioFilePath = Path.GetFullPath(Path.Combine(currentLevel.loadedPath, currentLevel.musicPath));
                var audioType = AudioTypeFinder.GetAudioTypeFromFileExtension(audioFilePath);

                if (audioType != AudioType.UNKNOWN)
                {
                    var handler = new DownloadHandlerAudioClip($"file://{audioFilePath}", audioType);
                    handler.compressed = true;

                    var request = new UnityWebRequest($"file://{audioFilePath}", "GET", handler, null);
                    await request.SendWebRequest();
                    if (request.responseCode == 200) _audioSource.clip = handler.audioClip;
                }
            }
            
            if (!_audioSource.clip)
            {
                throw new Exception($"Music Load Failed (Id: {currentLevel.musicPath})");
            }

            InitGame();
            isLoaded = true;
            onLoaded?.Invoke();
            
            if (playAtLoaded)
            {
                Play();
            }
        }

        private void InitGame()
        {
            Pause();
            _audioSource.Stop();

            foreach (var noteObj in noteObjects)
            {
                Destroy(noteObj.gameObject);
            }
            noteObjects.Clear();
            
            foreach (var note in currentLevel.pattern)
            {
                AddNoteObject(note);
            }
            
            currentBoss.InitHp(currentLevel.pattern.Count);

            AdjustBossPosition();

            _lastCheckpointBeat = float.NaN;
            _accDspTime = -2.0;
            _pauseDspTime = 0;
            _startDspTime = 0;
        }

        private NoteObject AddNoteObject(Note note)
        {
            var prefab = currentBoss.noteMap[note.noteType];
            var noteObj = Instantiate(prefab, transform, true);
                
            noteObjects.Add(noteObj);
            noteObj.note = note;

            return noteObj;
        }

        public NoteObject AddPattern(Note newNote)
        {
            currentLevel.pattern.Add(newNote);
            return AddNoteObject(newNote);
        }

        public LevelEvent AddEvent(LevelEvent levelEvent)
        {
            currentLevel.events.Add(levelEvent);
            return levelEvent;
        }

        private void AdjustBossPosition()
        {
            if(!currentBoss) return;
            
            currentBoss.transform.position = new Vector3(
                _camera.ViewportToWorldPoint(new Vector3(playingViewportEnd.x, 0)).x - 0.5f,
                0
            );
        }

        private void AdjustPlayerPosition()
        {
            if(!player) return;
            
            player.transform.position = new Vector3(
                _camera.ViewportToWorldPoint(new Vector3(playingViewportStart.x, 0)).x + 0.5f,
                0
            );
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
            
            _startDspTime = dspTime;
            _accDspTime = time;
            
            _audioSource.Stop();

            if(isCorrectTime) _audioSource.time = (float)time + offset + currentLevel.startOffset;
            
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
            return noteObjects
                .FindAll(noteObj => noteObj.note.appearBeat > currentBeat - 1f && !noteObj.wasHit)
                .OrderBy(noteObj => noteObj.note.appearBeat)
                .FirstOrDefault();
        }

        public void Play()
        {
            if(isPlaying) return;
            
            _startDspTime = dspTime;
            if (_accDspTime < 0f)
            {
                _audioSource.PlayScheduled(_startDspTime - _accDspTime - offset - currentLevel.startOffset);
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

        public JudgementType AddJudgement(float inputTime, float originTime, JudgementTimeSettings judgementTimeSettings)
        {
            var pos = Vector3.up * 1.4f;
            var offset = inputTime - originTime;
            var absOffset = Mathf.Abs(offset);
            var result = JudgementType.Miss;

            if (absOffset <= judgementTimeSettings.perfect)
            {
                perfectCount++;
                result = JudgementType.Perfect;
            }
            else if (absOffset <= judgementTimeSettings.good)
            {
                goodCount++;
                result = JudgementType.Good;
            }
            else if (absOffset <= judgementTimeSettings.bad)
            {
                if (offset < 0f)
                {
                    earlyCount++;
                    result = JudgementType.Early;
                }
                else
                {
                    lateCount++;
                    result = JudgementType.Late;
                }
            }
            else
            {
                missCount++;
                result = JudgementType.Miss;
            }
            
            LevelManager.instance.onAddJudgement?.Invoke(result);
            return result;
        } 
    }

    [Serializable]
    public class Level
    {
        [NonSerialized] public string loadedPath;
        
        public float startOffset;
        public int beatsPerMeasure;
        public int defaultBpm;
        public float baseScrollSpeed;
        public string bossId;
        public string musicPath;

        public string levelName;
        public string musicName;
        public string authorName;

        public string backgroundId;
        public string groundId;
        
        public List<Note> pattern;
        public List<LevelEvent> events;
    }

    [Serializable]
    public class LevelEvent
    {
        public float appearBeat;
        public bool? isCheckpoint;

        public LevelEvent Clone()
        {
            return new LevelEvent
            {
                appearBeat = appearBeat,
                isCheckpoint = isCheckpoint
            };
        }
    }

    [Serializable]
    public class Note
    {
        public float appearBeat;
        public string noteType;

        public Note Clone()
        {
            return new Note
            {
                appearBeat = appearBeat,
                noteType = noteType
            };
        }
    }

    [Serializable]
    public struct JudgementTimeSettings
    {
        public float miss;
        public float perfect;
        public float good;
        public float bad;
    }

    public enum JudgementType
    {
        Miss, Perfect, Good, Late, Early
    }
}