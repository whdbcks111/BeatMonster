using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _02.Scripts.Level;
using _02.Scripts.Level.Note;
using _02.Scripts.Settings;
using _02.Scripts.UI;
using _02.Scripts.Utils;
using _09.ScriptableObjects;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
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
        
        public float maxPlayTime => BeatToPlayTime(currentLevel.pattern
            .OrderByDescending(n => n.appearBeat)
            .FirstOrDefault()?.appearBeat ?? 0f);

        public float currentBeat => currentLevel == null ? -1f : currentPlayTime * currentLevel.defaultBpm / 60f;

        public float offset => GameSettings.settings.audioOffset;
        public Player player;
        public LevelPlayerData currentLevelPlayerData;
        [NonSerialized] public Level currentLevel;
        [NonSerialized] public Boss currentBoss;
        [NonSerialized] public readonly List<NoteObject> noteObjects = new();
        [NonSerialized] public bool isLoaded = false;

        [NonSerialized] public Action onLoaded;
        [NonSerialized] public Action<JudgementType> onAddJudgement;

        public bool isLevelEditor;
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

        private double _startDspTime = 0, _pauseDspTime = 0, _accDspTime = 0;
        private AudioSource _audioSource;
        private float _checkpointEnterTime = float.NaN;
        private CheckpointData _checkpointData;
        private Camera _camera;

        private double _prevDspTime, _interpolatedDspTime;

        private float _prepareEndBeat = 0f;
        private bool _isPreparing = false;
        private int _nextPrepareCountdownBeat = 0;

        private bool _isCleared = false;

        [Header("Checkpoint Shader Control")]
        [SerializeField] private Material checkpointMat;
        
        [Header("Background Objects")]
        [SerializeField] private SpriteRenderer groundObject;
        [SerializeField] private SpriteRenderer backgroundObject;
        
        [Header("GUI")]
        [SerializeField] private LoadingScreenGUI loadingScreenGUI;
        [SerializeField] private GameViewGUI gameViewGUI;

        [Header("Prepare Sounds")]
        [SerializeField] private AudioClip prepareSound;
        [SerializeField] private float prepareSoundVolume = 1f;
        
        private static readonly int CheckpointVfxRadius = Shader.PropertyToID("_Radius");
        private static readonly int CheckpointVfxCenter = Shader.PropertyToID("_Center");

        private const double AudioStartEpsilon = 0.01;

        private void Awake()
        {
            instance = this;
            
            GameSettings.Initialize();

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
            loadingScreenGUI.HideLoadingPanel();
            
            if (SceneTransitionData.levelData != null)
            {
                var level = SceneTransitionData.levelData;
                SceneTransitionData.levelData = null;
                LoadLevel(level).Forget();
            }
            else
            {
                // create new level
                LoadLevel(new Level()).Forget();
            }
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
            UpdateLevelEvents();
            UpdatePrepare();
            GameClearCheckUpdate();
        }

        private void GameClearCheckUpdate()
        {
            if (!gameViewGUI) return;
            
            if (currentPlayTime > maxPlayTime + 1f)
            {
                if (!_isCleared)
                {
                    gameViewGUI.OpenClearWindow();
                    if(!isLevelEditor) currentLevelPlayerData.SaveData(currentLevel.levelUuid);
                }
                _isCleared = true;
            }
            else
            {
                if(_isCleared) gameViewGUI.CloseClearWindow();
                _isCleared = false;
            }
        }

        private void UpdatePrepare()
        {
            if(currentLevel == null) return;

            if (_isPreparing)
            {
                var countdownAppearBeat = _prepareEndBeat - _nextPrepareCountdownBeat;
                var beatOffset = BeatToPlayTime(countdownAppearBeat - currentBeat) - offset;
                
                if (beatOffset <= offset)
                {
                    if (_nextPrepareCountdownBeat >= 0)
                    {
                        var playDspTime = dspTime + beatOffset;
                        SoundManager.instance.PlaySfxScheduled(prepareSound, playDspTime, prepareSoundVolume);
                    }
                    
                    _nextPrepareCountdownBeat--;
                }

                if (_prepareEndBeat <= currentBeat)
                {
                    _isPreparing = false;
                }
            }

            if (gameViewGUI)
            {
                var countdown = Mathf.FloorToInt(_prepareEndBeat - currentBeat) + 1;
                gameViewGUI.SetPrepareCountdown(countdown <= 4 ? countdown : -1);
            }
        }

        private void UpdateLevelEvents()
        {
            if(currentLevel == null) return;
            
            foreach (var evt in currentLevel.events.OrderBy(e => e.appearBeat))
            {
                if (!evt.isPerformed && evt.appearBeat <= currentBeat)
                {
                    PerformEvent(evt);
                }
            }
        }

        public void SetCheckpoint(float beat)
        {
            if (currentLevel == null) return;
            _checkpointData.beat = beat;
            _checkpointData.playerData = currentLevelPlayerData.Clone();
            _checkpointEnterTime = Time.unscaledTime + BeatToPlayTime(beat) - currentPlayTime;
        }

        public void GotoCheckpoint()
        {
            if (currentLevel == null) return;
            
            var prepareTime = BeatToPlayTime(4f) + 1f;
            _prepareEndBeat = _checkpointData?.beat ?? 0f;
            _nextPrepareCountdownBeat = 4;
            _isPreparing = true;
            Seek(_checkpointData == null ? 0f : BeatToPlayTime(_checkpointData.beat), prepareTime);
            player.SetPrepareVfx(_prepareEndBeat);
            
            if (_checkpointData != null)
            {
                var respawnCount = currentLevelPlayerData.respawnCount;
                currentLevelPlayerData = _checkpointData.playerData.Clone();
                currentLevelPlayerData.respawnCount = respawnCount;
            }
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
            if (currentLevel == null) return;
            if(string.IsNullOrEmpty(path)) return;
            
            var json = JsonConvert.SerializeObject(currentLevel, Formatting.Indented);
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
                JsonConvert.DeserializeObject<Level>(Resources.Load<TextAsset>(path[1..]).text) : 
                JsonConvert.DeserializeObject<Level>(await File.ReadAllTextAsync(path));
            level.loadedPath = path;
            await LoadLevel(level);
            Debug.Log($"Load level [{currentLevel.levelName}] from {path} / music id: {currentLevel.musicPath}");
        }

        public async UniTask LoadLevel(Level level)
        {
            print($"Load level [{level.levelUuid}]");
            
            currentLevel = level;
            if (editorBindings) editorBindings.level = currentLevel;


            await LoadBoss();
            InitGame();
            await LoadMusic();
            
            isLoaded = true;
            onLoaded?.Invoke();
            
            if (playAtLoaded)
            {
                Play();
            }
        }

        public async UniTask LoadBoss()
        {
            if(currentBoss) Destroy(currentBoss.gameObject);
            
            Boss bossObj = null;
            var loadedList = await Addressables.LoadAssetsAsync<GameObject>("boss", _ => { }).Task;
            foreach (var bossPrefab in loadedList)
            {
                var boss = bossPrefab.GetComponent<Boss>();
                if (boss.bossId == currentLevel.bossId)
                {
                    bossObj = Instantiate(boss, 
                        Camera.main.ViewportToWorldPoint(playingViewportEnd), Quaternion.identity, transform);
                }
            }
            if (!bossObj || !bossObj.TryGetComponent(out currentBoss))
            {
                throw new Exception($"Boss Load Failed (Id: {currentLevel.bossId})");
            }
        }

        public async UniTask LoadMusic()
        {
            _audioSource.clip = await GetAudioClipFromLevel(currentLevel);
        }

        public static async UniTask<AudioClip> GetAudioClipFromLevel(Level level)
        {
            if (level.musicPath.StartsWith("$"))
            {
                // music path is addressable asset key
                return await Addressables.LoadAssetAsync<AudioClip>($"Music/{level.musicPath[1..]}");
            }
            
            if(!string.IsNullOrEmpty(level.musicPath))
            {
                // music path is relative local audio file path
                
                var audioFilePath = Path.GetFullPath(Path.Combine(level.loadedPath ?? "", level.musicPath));
                var audioType = AudioTypeFinder.GetAudioTypeFromFileExtension(audioFilePath);

                if (audioType != AudioType.UNKNOWN)
                {
                    var handler = new DownloadHandlerAudioClip($"file://{audioFilePath}", audioType);
                    handler.compressed = false;
                    handler.streamAudio = false;

                    var request = new UnityWebRequest($"file://{audioFilePath}", "GET", handler, null);
                    await request.SendWebRequest();
                    if (request.responseCode == 200)
                    {
                        var clip = handler.audioClip;
                        clip.LoadAudioData();
                        return clip;
                    }
                }
            }

            return null;
        }

        private void InitGame()
        {
            if (currentLevel == null) return;
            Pause();
            _audioSource.Stop();

            RespawnNotes();
            
            foreach (var evt in currentLevel.events.OrderByDescending(e => e.appearBeat))
            {
                evt.Prevent();
            }
            
            currentBoss.InitHp(currentLevel.pattern.Count);

            _checkpointData = new()
            {
                beat = 0f,
                playerData = currentLevelPlayerData.Clone()
            };

            AdjustBossPosition();

            _prepareEndBeat = 0f;
            _nextPrepareCountdownBeat = 4;
            _isPreparing = true;
            
            _accDspTime = -(BeatToPlayTime(4f) + 2f);
            _pauseDspTime = 0;
            _startDspTime = 0;
        }

        public void RespawnNotes()
        {
            foreach (var noteObj in noteObjects)
            {
                DestroyImmediate(noteObj.gameObject);
            }
            noteObjects.Clear();
            
            foreach (var note in currentLevel.pattern)
            {
                AddNoteObject(note);
            }
        }

        public async UniTask ChangeBoss(Boss boss)
        {
            currentLevel.bossId = boss.bossId;
            await LoadBoss();
            
            foreach (var note in currentLevel.pattern)
            {
                if (!currentBoss.noteMap.ContainsKey(note.noteType))
                {
                    note.noteType = currentBoss.noteMap.First().Key;
                }
            }
            RespawnNotes();
        }

        public void ChangeBackground(Sprite sprite)
        {
            if (currentLevel == null) return;
            backgroundObject.sprite = sprite;
        }
        
        public void ChangeGround(Sprite sprite)
        {
            if (currentLevel == null) return;
            groundObject.sprite = sprite;
        }

        private NoteObject AddNoteObject(Note note)
        {
            if (currentLevel == null) return null;
            var prefab = currentBoss.noteMap[note.noteType];
            var noteObj = Instantiate(prefab, transform, true);
                
            noteObjects.Add(noteObj);
            noteObj.note = note;

            return noteObj;
        }

        public NoteObject AddPattern(Note newNote)
        {
            if (currentLevel == null) return null;
            currentLevel.pattern.Add(newNote);
            return AddNoteObject(newNote);
        }

        public LevelEvent AddEvent(LevelEvent levelEvent)
        {
            if (currentLevel == null) return null;
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
            if (currentLevel == null) return;
            SoundManager.instance.StopAllSfx();
            if (!isPlaying) return;
            
            _pauseDspTime = dspTime;
            _accDspTime += _pauseDspTime - _startDspTime;
            
            _audioSource.Stop();

            isPlaying = false;
        }

        public void Stop()
        {
            if (currentLevel == null) return;
            InitGame();
        }

        public void Seek(double time, float prepareTime = 0f)
        {
            if (currentLevel == null) return;
            
            SoundManager.instance.StopAllSfx();
            time -= prepareTime;
            
            _startDspTime = dspTime;
            _accDspTime = time;

            _audioSource.Stop();
            var scheduleDspTime = _startDspTime - offset - currentLevel.startOffset - time;
            if (scheduleDspTime < _startDspTime)
            {
                // 현재 시간보다 전에 시작
                var timeOffset = (float)(dspTime - scheduleDspTime + AudioStartEpsilon);
                if (_audioSource.clip)
                {
                    _audioSource.timeSamples = (int)( timeOffset * _audioSource.clip.frequency);
                }
                if (isPlaying)
                {
                    _audioSource.PlayScheduled(dspTime + AudioStartEpsilon);
                }
            }
            else if (isPlaying)
            {
                // 현재 시간 이후에 시작
                _audioSource.PlayScheduled(scheduleDspTime);
            }

            var bossHp = 0;
            foreach (var noteObj in noteObjects)
            {
                noteObj.wasHit = noteObj.note.appearBeat < currentBeat + PlayTimeToBeat(prepareTime - judgementTimeSettings.perfect);
                noteObj.canPlayHitSound = !noteObj.wasHit;
                if (noteObj.canPlayHitSound) ++bossHp;
            }

            foreach (var e in currentLevel.events.OrderByDescending(e => e.appearBeat))
            {
                var newPerformed = e.appearBeat <= currentBeat + PlayTimeToBeat(prepareTime);
                if (e.isPerformed && !newPerformed)
                {
                    e.Prevent();
                }
                else if (!e.isPerformed && newPerformed)
                {
                    PerformEvent(e);
                }
                else e.isPerformed = newPerformed;
            }

            player.ResetSkillBall();
            currentBoss.hp = bossHp;
        }

        public void PerformEvent(LevelEvent e)
        {
            if (currentLevel == null) return;
            e.isPerformed = true;
            if (e.isCheckpoint == true)
            {
                // 체크포인트 이벤트 실행
                var beforeCheckpoint = _checkpointData?.Clone();
                SetCheckpoint(e.appearBeat);
                if(gameViewGUI) gameViewGUI.TriggerCheckpointVFX().Forget();
                
                e.preventActions.Add(() =>
                {
                    var respawnCount = currentLevelPlayerData.respawnCount;
                    currentLevelPlayerData = _checkpointData.playerData.Clone();
                    currentLevelPlayerData.respawnCount = respawnCount;
                    
                    _checkpointData = beforeCheckpoint;
                });
            }
        }
        
        public NoteObject GetNextNote()
        {
            if (currentLevel == null) return null;
            // 안 친 노트중에 미스판정 이상인 노트
            var targets = noteObjects.FindAll(noteObj => 
                    BeatToPlayTime(noteObj.note.appearBeat) > currentPlayTime - judgementTimeSettings.bad 
                    && !noteObj.wasHit);

            if (targets.Count == 0) return null;

            var firstNote = targets[0];
            var minAppearBeat = firstNote.note.appearBeat;
            
            foreach (var noteObject in targets)
            {
                if (noteObject.note.appearBeat < minAppearBeat)
                {
                    firstNote = noteObject;
                    minAppearBeat = firstNote.note.appearBeat;
                }
            }
            return firstNote;
        }

        public void Play()
        {
            if (currentLevel == null) return;
            if(isPlaying) return;
            
            isPlaying = true;
            
            _startDspTime = dspTime;
            _audioSource.Stop();
            var scheduleDspTime = _startDspTime - offset - currentLevel.startOffset - currentPlayTime;
            if (scheduleDspTime < dspTime)
            {
                // 현재 시간보다 전에 시작
                var timeOffset = (float)(dspTime - scheduleDspTime + AudioStartEpsilon);
                if (_audioSource.clip)
                {
                    _audioSource.timeSamples = (int)( timeOffset * _audioSource.clip.frequency);
                }
                if (isPlaying && timeOffset < (_audioSource.clip?.length ?? 0f))
                {
                    _audioSource.PlayScheduled(dspTime + AudioStartEpsilon);
                }
            }
            else if (isPlaying)
            {
                // 현재 시간 이후에 시작
                _audioSource.PlayScheduled(scheduleDspTime);
            }
        }

        public float BeatToPlayTime(float beat)
        {
            if (currentLevel == null) return 0f;
            return beat * 60f / currentLevel.defaultBpm;
        }

        public float PlayTimeToBeat(float time)
        {
            if (currentLevel == null) return 0f;
            return time / 60f * currentLevel.defaultBpm;
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

        public LevelPlayerData Clone()
        {
            var json = JsonConvert.SerializeObject(this);
            return JsonConvert.DeserializeObject<LevelPlayerData>(json);
        }

        public void SaveData(string levelId)
        {
            var json = JsonConvert.SerializeObject(this);
            var path = Path.Combine(Application.persistentDataPath, "PlayData", $"{levelId}.json");
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                var dirPath = Path.GetDirectoryName(path);
                if(dirPath != null) Directory.CreateDirectory(dirPath);
            }
            File.WriteAllText(path, json);
        }

        public float GetAccuracy()
        {
            var score = perfectCount + goodCount * 0.8f + (lateCount + earlyCount) * 0.5f;
            var maxScore = perfectCount + goodCount + lateCount + earlyCount + missCount;

            if (Mathf.Approximately(maxScore, 0f)) return 0f;
            
            return score / maxScore * 100f;
        }

        public int GetStarCount()
        {
            var acc = GetAccuracy();
            var result = 0;

            if (acc >= 80f) result++;
            if (acc >= 90f) result++;
            if (acc >= 95f) result++;
            if (respawnCount == 0) result++;
            if (missCount == 0) result++;
            
            return result;
        }
        
        public bool IsPerfectClear() => GetAccuracy() >= 100f;

        public JudgementType GetJudgement(float inputTime, float originTime,
            JudgementTimeSettings judgementTimeSettings)
        {
            
            var pos = Vector3.up * 1.4f;
            var offset = inputTime - originTime;
            var absOffset = Mathf.Abs(offset);
            var result = JudgementType.Miss;

            if (absOffset <= judgementTimeSettings.perfect)
            {
                result = JudgementType.Perfect;
            }
            else if (absOffset <= judgementTimeSettings.good)
            {
                result = JudgementType.Good;
            }
            else if (absOffset <= judgementTimeSettings.bad)
            {
                if (offset < 0f)
                {
                    result = JudgementType.Early;
                }
                else
                {
                    result = JudgementType.Late;
                }
            }

            return result;
        }

        public JudgementType AddJudgement(float inputTime, float originTime, 
            JudgementTimeSettings judgementTimeSettings)
        {
            return AddJudgement(GetJudgement(inputTime, originTime, judgementTimeSettings));
        }
        
        public JudgementType AddJudgement(JudgementType result)
        {

            switch (result)
            {
                case JudgementType.Miss:
                    missCount++;
                    break;
                case JudgementType.Perfect:
                    perfectCount++;
                    break;
                case JudgementType.Good:
                    goodCount++;
                    break;
                case JudgementType.Late:
                    lateCount++;
                    break;
                case JudgementType.Early:
                    earlyCount++;
                    break;
                default:
                    missCount++;
                    break;
            }
            
            LevelManager.instance.onAddJudgement?.Invoke(result);
            return result;
        } 
    }

    [Serializable]
    public class Level
    {
        [NonSerialized] public string loadedPath;
        
        public string levelUuid = Guid.NewGuid().ToString();
        
        public float startOffset = 0f;
        public int beatsPerMeasure = 4;
        public int defaultBpm = 120;
        public float baseScrollSpeed = 5.0f;
        public string bossId = "Slime";
        public string musicPath = "";

        public string levelName = "Untitled";
        public string musicName = "Untitled";
        public string authorName = "Anonymous";

        public string backgroundId = "Sky";
        public string groundId = "Ground";
        
        public List<Note> pattern = new();
        public List<LevelEvent> events = new();
    }

    [Serializable]
    public class LevelEvent
    {
        public float appearBeat;
        public bool? isCheckpoint;

        [NonSerialized] public bool isPerformed = false;
        [NonSerialized] public List<Action> preventActions = new();

        public LevelEvent Clone()
        {
            return new LevelEvent
            {
                appearBeat = appearBeat,
                isCheckpoint = isCheckpoint
            };
        }

        public void Prevent()
        {
            isPerformed = false;
            for (var i = preventActions.Count - 1; i >= 0; i--)
            {
                var action = preventActions[i];
                action.Invoke();
            }
            preventActions.Clear();
        }
    }

    [Serializable]
    public class CheckpointData
    {
        public float beat;
        public LevelPlayerData playerData;
        
        public CheckpointData Clone()
        {
            return new CheckpointData
            {
                beat = beat,
                playerData = playerData.Clone()
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