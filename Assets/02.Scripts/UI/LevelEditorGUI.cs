using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using _02.Scripts.Level;
using _02.Scripts.Level.Note;
using _02.Scripts.Manager;
using _02.Scripts.ScriptableObjects;
using _02.Scripts.Utils;
using Cysharp.Threading.Tasks;
using SFB;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
// ReSharper disable MemberCanBePrivate.Global

namespace _02.Scripts.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class LevelEditorGUI : MonoBehaviour
    {
        private UIDocument _uiDocument;
        private VisualElement _root;

        public VisualTreeAsset timeGuideTemplate;
        public VisualTreeAsset snapGuideTemplate;
        public VisualTreeAsset noteTemplate;
        public VisualTreeAsset eventTemplate;
        public VisualTreeAsset selectWindowElementTemplate;

        [NonSerialized] public Button playInGameBtn, stopBtn, playAndPauseBtn, autoPlayBtn, 
            zoomOutBtn, zoomInBtn,
            openBtn, saveBtn, saveAsBtn, exitBtn,
            addNoteBtn, addEventBtn, duplicateBtn, copyBtn, pasteBtn,
            delBtn, moveLeftBtn, moveRightBtn,
            menuSnapEnableBtn, snapEnableBtn,
            musicFileLoadBtn, bossChangeBtn, bgChangeBtn, groundChangeBtn, quantizeBtn,
            noteTypeChangeBtn;

        [NonSerialized] public Label curTimeLabel, curBeatLabel, toastLabel, 
            currentBossNameLabel, currentBgNameLabel, currentGroundNameLabel,
            musicFilePathLabel, currentNoteTypeLabel;

        [NonSerialized] public VisualElement timePicker, 
            noteTrack, eventTrack, 
            timeline, timeChangeZone, timeGuideContainer, snapGuideContainer,
            noteSelectionBox, eventSelectionBox,
            selectWindow, noteImageVisualElement;

        private ScrollView _selectWindowScrollView;
        private readonly List<VisualElement> _selectWindowRows = new();
        private readonly List<VisualElement> _selectWindowElements = new();

        [NonSerialized] private VisualElement _gameViewVisualElement;

        [NonSerialized] public VisualElement levelMenuSelector, propsMenuSelector, editorMenuSelector;
        [NonSerialized] public VisualElement levelMenu, propsMenu, editorMenu;

        [NonSerialized] public VisualElement notePropsMenu, eventPropsMenu;

        [NonSerialized] public DropdownField snapSizeDropdown;

        [NonSerialized] public Toggle checkpointToggle;

        [NonSerialized] public TextField startOffsetTextField, bpmTextField, snapSizeTextField,
            measureTextField, noteScrollSpeedTextField, noteAppearBeatTextField;

        [SerializeField] private float normalBeatGuidelineHideSpan = 15f, subBeatGuidelineHideSpan = 70f;
        [SerializeField] private float snapSpaceSize = 0.3f;
        
        [SerializeField] private LoadingScreenGUI loadingScreenGUI;

        [Header("SFX")] 
        [SerializeField] private AudioClip buttonSoundClip;
        [SerializeField] private float buttonSoundVolume = 1f;
        
        private bool _isCurrentTimeChanging = false;
        private bool _isTimelineDragging = false;
        private bool _isNoteTrackDragging = false;
        private bool _isEventTrackDragging = false;
        private bool _isSnapEnabled = false;
        private bool _isNoteAppearBeatChanging = false;
        
        private float _horizontalGuideSpan = 100f;
        [NonSerialized] public float currentLeftEndBeat = -5f;
        public float snapSize { get; private set; } = 0.25f;

        private float _timelineDragStartX, _timelineDragStartLeftEndBeat;

        private Vector2 _noteTrackDragStartPos, _eventTrackDragStartPos;

        private readonly List<VisualElement> _timeGuideLines = new();
        private readonly List<VisualElement> _snapGuideLines = new();
        private readonly List<Note> _copiedNotes = new();
        private readonly List<LevelEvent> _copiedEvents = new();
        
        public readonly List<NoteElementWrapper> noteWrappers = new();
        public readonly List<EventElementWrapper> eventWrappers = new();

        private const int SelectWindowColumnCount = 3;
        
        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            _root = _uiDocument.rootVisualElement;

            BindElements();
            RegisterEvents().Forget();
        }

        private void Update()
        {
            AdjustCameraToGameView();

            TimeGuidelineRenderUpdate();
            SnapGuidelineRenderUpdate();
            NoteRenderUpdate();
            EventRenderUpdate();

            UIUpdate();
        }

        private void UIUpdate()
        {
            if (!LevelManager.instance) return;
            
            if (LevelManager.instance.player.autoPlay) autoPlayBtn.AddToClassList("active");
            else autoPlayBtn.RemoveFromClassList("active");

            if (_isSnapEnabled)
            {
                snapEnableBtn.AddToClassList("active");
                menuSnapEnableBtn.AddToClassList("active");
            }
            else
            {
                snapEnableBtn.RemoveFromClassList("active");
                menuSnapEnableBtn.RemoveFromClassList("active");
            }
            
            var isCustomSnapSize = !snapSizeDropdown.value.StartsWith("1/");
            snapSizeTextField.style.display = isCustomSnapSize ? DisplayStyle.Flex : DisplayStyle.None;

            if (LevelManager.instance.currentBoss)
            {
                currentBossNameLabel.text = LevelManager.instance.currentBoss.bossName;
            }
            
            if (LevelManager.instance.currentLevel != null)
            {
                var currentPlayTime = LevelManager.instance.currentPlayTime;
                var currentBeat = LevelManager.instance.currentBeat;
                var measure = LevelManager.instance.currentLevel.beatsPerMeasure;
                
                musicFilePathLabel.text = LevelManager.instance.currentLevel.musicPath;
                currentGroundNameLabel.text = LevelManager.instance.currentLevel.groundId;
                currentBgNameLabel.text = LevelManager.instance.currentLevel.backgroundId;
                
                timePicker.style.left =
                    new StyleLength(new Length(
                        (currentBeat - currentLeftEndBeat) * _horizontalGuideSpan, 
                        LengthUnit.Pixel));
                timePicker.style.display =
                    LevelManager.instance.currentLevel == null ? DisplayStyle.None : DisplayStyle.Flex;
                if(curTimeLabel != null) curTimeLabel.text = 
                    $"{(currentPlayTime < 0f ? "-":"")}{(int)Mathf.Abs(currentPlayTime / 60) :D2}:{(int)Mathf.Abs(currentPlayTime % 60) :D2}";
                if(curBeatLabel != null) curBeatLabel.text =
                    $"{Mathf.FloorToInt(currentBeat / measure)} bar {Mathf.FloorToInt(ExtraMath.PositiveMod(currentBeat, measure))} beat";
            }
            
            var selectedNotes = noteWrappers.FindAll(w => w.isSelected);
            var selectedEvents = eventWrappers.FindAll(w => w.isSelected);
            notePropsMenu.style.display = selectedNotes.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            eventPropsMenu.style.display = selectedEvents.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            
            if (selectedNotes.Count > 0)
            {
                if (!_isNoteAppearBeatChanging)
                {
                    noteAppearBeatTextField.value = selectedNotes.All(
                        n => Mathf.Approximately(
                            n.noteObject.note.appearBeat, 
                            selectedNotes[0].noteObject.note.appearBeat
                        )
                    ) ? $"{selectedNotes[0].noteObject.note.appearBeat:F3}" : "-";
                }
                currentNoteTypeLabel.text = selectedNotes.All(
                    n => n.noteObject.note.noteType 
                         == selectedNotes[0].noteObject.note.noteType
                ) ? selectedNotes[0].noteObject.note.noteType : "-";
                noteImageVisualElement.style.backgroundImage = currentNoteTypeLabel.text == "-"
                    ? null
                    : selectedNotes[0].noteObject.spriteRenderer.sprite.texture;
            }

            if (selectedEvents.Count > 0)
            {
                checkpointToggle.value = selectedEvents.All(
                    e => e.levelEvent.isCheckpoint 
                         == selectedEvents[0].levelEvent.isCheckpoint) && (selectedEvents[0].levelEvent.isCheckpoint ?? false);
            }
        }

        private void AdjustCameraToGameView()
        {
            if (!LevelManager.instance) return;
            var width = _gameViewVisualElement.resolvedStyle.width / Screen.width;
            var height = _gameViewVisualElement.resolvedStyle.height / Screen.height;
            
            if(float.IsNaN(width) || float.IsNaN(height)) return;
            
            LevelManager.instance.playingViewportStart = new Vector2(
                0f,
                1f - height
            );
            LevelManager.instance.playingViewportEnd = new Vector2(
                width,
                1f
            );
        }

        private void NoteRenderUpdate()
        {
            if (!LevelManager.instance) return;
            var noteObjectCount = LevelManager.instance.noteObjects.Count;

            if (noteWrappers.Count > noteObjectCount)
            {
                var removeCount = noteWrappers.Count - noteObjectCount;
                for (var i = 0; i < removeCount; i++)
                {
                    noteTrack.Remove(noteWrappers[i].element);
                }
                noteWrappers.RemoveRange(0, removeCount);
            }
            else if (noteWrappers.Count < noteObjectCount)
            {
                var addCount = noteObjectCount - noteWrappers.Count;
                for (var i = 0; i < addCount; i++)
                {
                    var noteElement = noteTemplate.Instantiate() as VisualElement;
                    foreach (var j in noteElement.Children())
                    {
                        noteElement = j;
                        break;
                    }
                    
                    noteTrack.Add(noteElement);
                    var wrapper = new NoteElementWrapper { element = noteElement };
                    noteWrappers.Add(wrapper);
                    
                    noteElement.RegisterCallback<PointerDownEvent>(e => OnClickNote(e, wrapper));
                }
            }

            for (var i = 0; i < noteWrappers.Count; i++)
            {
                var noteObj = noteWrappers[i].noteObject = LevelManager.instance.noteObjects[i];
                var element = noteWrappers[i].element;

                element.style.backgroundImage = noteObj.spriteRenderer.sprite.texture;
                element.style.left = (noteObj.note.appearBeat - currentLeftEndBeat) * _horizontalGuideSpan;

                if (noteWrappers[i].isSelected || noteWrappers[i].isInSelectionBox)
                {
                    element.AddToClassList("selected");
                }
                else
                {
                    element.RemoveFromClassList("selected");
                }
            }
        }
        
        private void EventRenderUpdate()
        {
            if (!LevelManager.instance || LevelManager.instance.currentLevel == null) return;
            var eventCount = LevelManager.instance.currentLevel.events.Count;

            if (eventWrappers.Count > eventCount)
            {
                var removeCount = eventWrappers.Count - eventCount;
                for (var i = 0; i < removeCount; i++)
                {
                    eventTrack.Remove(eventWrappers[i].element);
                }
                eventWrappers.RemoveRange(0, removeCount);
            }
            else if (eventWrappers.Count < eventCount)
            {
                var addCount = eventCount - eventWrappers.Count;
                for (var i = 0; i < addCount; i++)
                {
                    var eventElement = eventTemplate.Instantiate() as VisualElement;
                    foreach (var j in eventElement.Children())
                    {
                        eventElement = j;
                        break;
                    }
                    
                    eventTrack.Add(eventElement);
                    var wrapper = new EventElementWrapper { element = eventElement };
                    eventWrappers.Add(wrapper);
                    
                    eventElement.RegisterCallback<PointerDownEvent>(e => OnClickEventElement(e, wrapper));
                }
            }

            for (var i = 0; i < eventWrappers.Count; i++)
            {
                var levelEvent = eventWrappers[i].levelEvent = LevelManager.instance.currentLevel.events[i];
                var element = eventWrappers[i].element;

                element.style.left = (levelEvent.appearBeat - currentLeftEndBeat) * _horizontalGuideSpan;

                if (eventWrappers[i].isSelected || eventWrappers[i].isInSelectionBox)
                {
                    element.AddToClassList("selected");
                }
                else
                {
                    element.RemoveFromClassList("selected");
                }
            }
        }

        private void TimeGuidelineRenderUpdate()
        {
            if (!LevelManager.instance) return;
            var neededGuideLineCount = Mathf.FloorToInt(timeChangeZone.resolvedStyle.width / _horizontalGuideSpan + 2) * 4;
            if (LevelManager.instance.currentLevel == null) neededGuideLineCount = 0;
            
            if (_timeGuideLines.Count > neededGuideLineCount && neededGuideLineCount >= 0)
            {
                var removeCount = _timeGuideLines.Count - neededGuideLineCount;
                for (var i = 0; i < removeCount; i++)
                {
                    timeGuideContainer.Remove(_timeGuideLines[i]);
                }
                _timeGuideLines.RemoveRange(0, removeCount);
            }
            else if (_timeGuideLines.Count < neededGuideLineCount)
            {
                var addCount = neededGuideLineCount - _timeGuideLines.Count;
                for (var i = 0; i < addCount; i++)
                {
                    var guideLine = timeGuideTemplate.Instantiate() as VisualElement;
                    foreach (var j in guideLine.Children())
                    {
                        guideLine = j;
                        break;
                    }
                    timeGuideContainer.Add(guideLine);
                    _timeGuideLines.Add(guideLine);
                }
            }

            if (LevelManager.instance.currentLevel != null)
            {
                var measure = LevelManager.instance.currentLevel.beatsPerMeasure;
            
                var firstGuidelineBeat = -ExtraMath.PositiveMod(currentLeftEndBeat, 1f);
                for (var i = 0; i < _timeGuideLines.Count; i++)
                {
                    var guideLine = _timeGuideLines[i];
                    var label = guideLine.Q<Label>("TimeLabel");
                    var beat = currentLeftEndBeat + i / 4f;

                    var displayBar = Mathf.FloorToInt(beat / measure);
                    var displayBeat = Mathf.FloorToInt(ExtraMath.PositiveMod(beat, measure));
                    guideLine.style.left = BeatToTimelineScale(firstGuidelineBeat) + i / 4f * _horizontalGuideSpan;
                    label.text = $"{displayBar}.{displayBeat}";
                    
                    if (i % 4 != 0 || _horizontalGuideSpan < subBeatGuidelineHideSpan && i % 4 == 0 && displayBeat != 0)
                    {
                        label.text = "";
                    }

                    if (displayBeat == 0 && i % 4 == 0)
                    {
                        guideLine.AddToClassList("on-beat");
                        guideLine.RemoveFromClassList("hidden");
                        guideLine.RemoveFromClassList("sub-beat");
                    }
                    else
                    {
                        if((_horizontalGuideSpan < normalBeatGuidelineHideSpan && i % 4 == 0) || 
                            (_horizontalGuideSpan < subBeatGuidelineHideSpan && i % 4 != 0)) 
                            guideLine.AddToClassList("hidden");
                        else guideLine.RemoveFromClassList("hidden");

                        if (i % 4 != 0) guideLine.AddToClassList("sub-beat");
                        else guideLine.RemoveFromClassList("sub-beat");
                        
                        guideLine.RemoveFromClassList("on-beat");
                    }
                }
            }
        }
        
        private void SnapGuidelineRenderUpdate()
        {
            snapGuideContainer.visible = _isSnapEnabled;
            
            if (!LevelManager.instance) return;
            var isCustomSnapSize = !snapSizeDropdown.value.StartsWith("1/");
            if (!isCustomSnapSize)
            {
                var isTriplet = snapSizeDropdown.value.EndsWith("T");
                if (float.TryParse(snapSizeDropdown.value[2..].Replace("T", ""), out var value))
                {
                    var beat = 1 / value;
                    if (isTriplet) beat *= 2 / 3f;
                    snapSize = beat;
                }

                snapSizeTextField.value = $"{snapSize:F10}".TrimEnd('0', '.');
            }
            
            var neededGuideLineCount = Mathf.FloorToInt((timeChangeZone.resolvedStyle.width / _horizontalGuideSpan + 2) / snapSize) ;
            
            if (_snapGuideLines.Count > neededGuideLineCount && neededGuideLineCount >= 0)
            {
                var removeCount = _snapGuideLines.Count - neededGuideLineCount;
                for (var i = 0; i < removeCount; i++)
                {
                    snapGuideContainer.Remove(_snapGuideLines[i]);
                }
                _snapGuideLines.RemoveRange(0, removeCount);
            }
            else if (_snapGuideLines.Count < neededGuideLineCount)
            {
                var addCount = neededGuideLineCount - _snapGuideLines.Count;
                for (var i = 0; i < addCount; i++)
                {
                    var guideLine = snapGuideTemplate.Instantiate() as VisualElement;
                    foreach (var j in guideLine.Children())
                    {
                        guideLine = j;
                        break;
                    }
                    snapGuideContainer.Add(guideLine);
                    _snapGuideLines.Add(guideLine);
                }
            }

            if (LevelManager.instance.currentLevel != null)
            {
                var firstGuidelineBeat = -ExtraMath.PositiveMod(currentLeftEndBeat, snapSize);
                for (var i = 0; i < _snapGuideLines.Count; i++)
                {
                    var guideLine = _snapGuideLines[i];
                    
                    guideLine.style.left = BeatToTimelineScale(firstGuidelineBeat) + i * snapSize * _horizontalGuideSpan;
                }
            }
        }

        private void BindElements()
        {
            _gameViewVisualElement = _root.Q<VisualElement>("GameView");
            
            playInGameBtn = _root.Q<Button>("PlayInGameButton");
            stopBtn = _root.Q<Button>("StopButton");
            playAndPauseBtn = _root.Q<Button>("PlayAndPauseButton");
            autoPlayBtn = _root.Q<Button>("AutoPlayButton");
            
            zoomOutBtn = _root.Q<Button>("ZoomOutButton");
            zoomInBtn = _root.Q<Button>("ZoomInButton");

            openBtn = _root.Q<Button>("OpenButton");
            saveBtn = _root.Q<Button>("SaveButton");
            saveAsBtn = _root.Q<Button>("SaveAsButton");
            exitBtn = _root.Q<Button>("ExitButton");
            
            addNoteBtn = _root.Q<Button>("AddNoteButton");
            addEventBtn = _root.Q<Button>("AddEventButton");
            duplicateBtn = _root.Q<Button>("DuplicateNoteButton");
            copyBtn = _root.Q<Button>("CopyButton");
            pasteBtn = _root.Q<Button>("PasteButton");
            
            delBtn = _root.Q<Button>("DeleteButton");
            moveLeftBtn = _root.Q<Button>("MoveLeftButton");
            moveRightBtn = _root.Q<Button>("MoveRightButton");

            curTimeLabel = _root.Q<Label>("TimeDisplay");
            curBeatLabel = _root.Q<Label>("BeatDisplay");

            timePicker = _root.Q<VisualElement>("TimePicker");
            noteTrack = _root.Q<VisualElement>("NoteTrack");
            eventTrack = _root.Q<VisualElement>("EventTrack");
            timeline = _root.Q<VisualElement>("Timeline");
            timeChangeZone = _root.Q<VisualElement>("TimeChangeZone");

            timeGuideContainer = _root.Q<VisualElement>("TimeGuideContainer");
            snapGuideContainer = _root.Q<VisualElement>("SnapGuideContainer");

            levelMenuSelector = _root.Q<VisualElement>("LevelMenuSelector");
            propsMenuSelector = _root.Q<VisualElement>("PropsMenuSelector");
            editorMenuSelector = _root.Q<VisualElement>("EditorMenuSelector");

            levelMenu = _root.Q<VisualElement>("LevelMenu");
            propsMenu = _root.Q<VisualElement>("PropsMenu");
            editorMenu = _root.Q<VisualElement>("EditorMenu");
            
            notePropsMenu = _root.Q<VisualElement>("NotePropsMenu");
            eventPropsMenu = _root.Q<VisualElement>("EventPropsMenu");

            musicFilePathLabel = _root.Q<Label>("MusicFilePathLabel");
            currentBossNameLabel = _root.Q<Label>("CurrentBossNameLabel");
            currentBgNameLabel = _root.Q<Label>("CurrentBGNameLabel");
            currentGroundNameLabel = _root.Q<Label>("CurrentGroundNameLabel");
            
            startOffsetTextField = _root.Q<TextField>("StartOffsetTextField");
            bpmTextField = _root.Q<TextField>("BPMTextField");

            noteSelectionBox = _root.Q<VisualElement>("NoteSelectionBox");
            eventSelectionBox = _root.Q<VisualElement>("EventSelectionBox");

            toastLabel = _root.Q<Label>("ToastLabel");
            snapEnableBtn = _root.Q<Button>("SnapEnableButton");
            menuSnapEnableBtn = _root.Q<Button>("MenuSnapActiveBtn");
            
            musicFileLoadBtn = _root.Q<Button>("MusicFileLoadBtn");
            bossChangeBtn = _root.Q<Button>("BossChangeBtn");
            bgChangeBtn = _root.Q<Button>("BGChangeBtn");
            groundChangeBtn = _root.Q<Button>("GroundChangeBtn");

            snapSizeDropdown = _root.Q<DropdownField>("SnapSizeDropdownField");
            snapSizeTextField = _root.Q<TextField>("SnapSizeTextField");
            snapSizeDropdown.value = "1/4";
            
            quantizeBtn = _root.Q<Button>("QuantizeBtn");
            
            noteAppearBeatTextField = _root.Q<TextField>("NoteAppearBeatTextField");
            noteTypeChangeBtn = _root.Q<Button>("NoteTypeChangeBtn");
            noteImageVisualElement = _root.Q<VisualElement>("NoteImage");
            currentNoteTypeLabel = _root.Q<Label>("CurrentNoteTypeLabel");

            measureTextField = _root.Q<TextField>("MeasureTextField");
            noteScrollSpeedTextField = _root.Q<TextField>("NoteScrollSpeedTextField");
            
            selectWindow = _root.Q<VisualElement>("SelectWindow");
            _selectWindowScrollView = selectWindow.Q<ScrollView>();
            
            // 이벤트 속성창 요소
            checkpointToggle = _root.Q<Toggle>("CheckpointToggle");
        }

        private async UniTask RegisterEvents()
        {
            playInGameBtn.clicked += PlayInGame;
            playAndPauseBtn.clicked += TogglePause;
            stopBtn.clicked += () => LevelManager.instance.Stop();

            zoomInBtn.clicked += () => _horizontalGuideSpan /= 1.1f;
            zoomOutBtn.clicked += () => _horizontalGuideSpan *= 1.1f;

            moveLeftBtn.clicked += OnClickMoveLeftBtn;
            moveRightBtn.clicked += OnClickMoveRightBtn;
            
            addNoteBtn.clicked += AddNote;
            addEventBtn.clicked += AddEvent;
            delBtn.clicked += DeleteSelectedElements;
            duplicateBtn.clicked += DuplicateElements;
            copyBtn.clicked += CopyElements;
            pasteBtn.clicked += PasteElements;

            saveBtn.clicked += SaveLevel;
            saveAsBtn.clicked += SaveLevelAs;
            openBtn.clicked += OpenLevel;
            exitBtn.clicked += GoToTitle;
            
            timeChangeZone.RegisterCallback<PointerDownEvent>(OnPointerDownInTimeChangeZone);
            timeline.RegisterCallback<PointerDownEvent>(OnPointerDownInTimeline);
            _root.RegisterCallback<PointerMoveEvent>(OnPointerMoveInRoot);
            _root.RegisterCallback<PointerUpEvent>(OnPointerUpInRoot);
            
            levelMenuSelector.RegisterCallback<ClickEvent>(_ => SelectMenu(levelMenuSelector, levelMenu));
            editorMenuSelector.RegisterCallback<ClickEvent>(_ => SelectMenu(editorMenuSelector, editorMenu));
            propsMenuSelector.RegisterCallback<ClickEvent>(_ => SelectMenu(propsMenuSelector, propsMenu));
            
            snapEnableBtn.clicked += ToggleSnap;
            menuSnapEnableBtn.clicked += ToggleSnap;

            quantizeBtn.clicked += QuantizeSelected;
            
            musicFileLoadBtn.clicked += LoadMusicFile;

            bpmTextField.RegisterCallback<FocusOutEvent>(OnFocusOutBpmTextField);
            startOffsetTextField.RegisterCallback<FocusOutEvent>(OnFocusOutStartOffsetTextField);
            snapSizeTextField.RegisterCallback<FocusOutEvent>(OnFocusOutSnapSizeTextField);
            
            measureTextField.RegisterCallback<FocusOutEvent>(OnFocusOutMeasureTextField);
            noteScrollSpeedTextField.RegisterCallback<FocusOutEvent>(OnFocusOutNoteScrollSpeedTextField);
            noteAppearBeatTextField.RegisterCallback<FocusOutEvent>(OnFocusOutNoteAppearBeatTextField);
            noteAppearBeatTextField.RegisterCallback<FocusInEvent>(OnFocusInNoteAppearBeatTextField);
            noteTypeChangeBtn.clicked += OnClickNoteChangeBtn;

            autoPlayBtn.clicked += ToggleAutoPlay;
            
            noteTrack.RegisterCallback<PointerDownEvent>(OnPointerDownInNoteTrack);
            eventTrack.RegisterCallback<PointerDownEvent>(OnPointerDownInEventTrack);

            bossChangeBtn.clicked += OnClickBossChangeBtn;
            bgChangeBtn.clicked += OnClickBGChangeBtn;
            groundChangeBtn.clicked += OnClickGroundChangeBtn;

            checkpointToggle.RegisterValueChangedCallback(OnCheckpointToggleValueChanged);
            
            foreach (var button in _root.Query<Button>().ToList())
            {
                button.clicked += OnClickButton;
            }

            await UniTask.WaitUntil(() => LevelManager.instance != null);

            LevelManager.instance.onLoaded += () =>
            {
                startOffsetTextField.value = $"{LevelManager.instance.currentLevel.startOffset}";
                bpmTextField.value = $"{LevelManager.instance.currentLevel.defaultBpm}";
                measureTextField.value = $"{LevelManager.instance.currentLevel.beatsPerMeasure}";
                noteScrollSpeedTextField.value = $"{LevelManager.instance.currentLevel.baseScrollSpeed:0.0}";
            };
        }

        private void GoToTitle()
        {
            loadingScreenGUI.ShowLoadingPanel(() =>
            {
                SceneManager.LoadSceneAsync("TitleScene");
            });
        }

        private void OnCheckpointToggleValueChanged(ChangeEvent<bool> evt)
        {
            var selectedEvents = eventWrappers.FindAll(w => w.isSelected);
            
            selectedEvents.ForEach(w => w.levelEvent.isCheckpoint = evt.newValue);
        }

        private void OnClickNoteChangeBtn()
        {
            ClearSelectWindow();
            
            foreach (var noteObj in LevelManager.instance.currentBoss.noteMap.Values)
            {
                AddSelectWindowElement(noteObj.noteType, noteObj.spriteRenderer.sprite, evt =>
                {
                    var selectedNotes = noteWrappers.FindAll(w => w.isSelected);
                    foreach (var w in selectedNotes)
                    {
                        w.noteObject.note.noteType = noteObj.noteType;
                        LevelManager.instance.RespawnNotes();
                    }
                    HideSelectWindow();
                });
            }
            
            FillSelectWindowRowEmptySpace();
            ShowSelectWindow();
        }

        private void OnFocusInNoteAppearBeatTextField(FocusInEvent _)
        {
            _isNoteAppearBeatChanging = true;
        }

        private void OnFocusOutNoteAppearBeatTextField(FocusOutEvent _)
        {
            var selectedNotes = noteWrappers.FindAll(w => w.isSelected);
            if (float.TryParse(noteAppearBeatTextField.value, out var beat))
            {
                foreach (var noteElementWrapper in selectedNotes)
                {
                    noteElementWrapper.noteObject.note.appearBeat = beat;
                }
            }
            
            _isNoteAppearBeatChanging = false;
        }

        public void QuantizeSelected()
        {
            foreach (var w in noteWrappers.Where(n => n.isSelected))
            {
                w.noteObject.note.appearBeat = ToQuantizedBeat(w.noteObject.note.appearBeat, false);
            }
            foreach (var w in eventWrappers.Where(e => e.isSelected))
            {
                w.levelEvent.appearBeat = ToQuantizedBeat(w.levelEvent.appearBeat, false);
            }
        }

        private void OnClickGroundChangeBtn()
        {
            ClearSelectWindow();

            Addressables.LoadAssetsAsync<GroundData>("ground", data =>
            {
                AddSelectWindowElement(data.groundName, data.sprite, evt =>
                {
                    LevelManager.instance.ChangeGround(data.sprite);
                    HideSelectWindow();
                });
            }).Completed += _ =>
            {
                FillSelectWindowRowEmptySpace();
                ShowSelectWindow();
            };
        }

        private void OnClickBGChangeBtn()
        {
            ClearSelectWindow();

            Addressables.LoadAssetsAsync<BackgroundData>("background", data =>
            {
                AddSelectWindowElement(data.backgroundName, data.sprite, evt =>
                {
                    LevelManager.instance.ChangeBackground(data.sprite);
                    HideSelectWindow();
                });
            }).Completed += _ =>
            {
                FillSelectWindowRowEmptySpace();
                ShowSelectWindow();
            };
        }

        private void OnClickBossChangeBtn()
        {
            ClearSelectWindow();

            Addressables.LoadAssetsAsync<GameObject>("boss", bossObj =>
            {
                var boss = bossObj.GetComponent<Boss>();
                AddSelectWindowElement(boss.bossName, boss.displaySprite, evt =>
                {
                    LevelManager.instance.ChangeBoss(boss).Forget();
                    HideSelectWindow();
                });
            }).Completed += _ =>
            {
                FillSelectWindowRowEmptySpace();
                ShowSelectWindow();
            };
        }

        private void ShowSelectWindow()
        {
            selectWindow.style.display = DisplayStyle.Flex;
        }

        private void HideSelectWindow()
        {
            selectWindow.style.display = DisplayStyle.None;
        }

        private void FillSelectWindowRowEmptySpace()
        {
            while (_selectWindowElements.Count % 3 != 0)
            {
                AddSelectWindowElement("", null, null, true);
            }
        }

        private void ClearSelectWindow()
        {
            _selectWindowScrollView.contentContainer.Clear();
            _selectWindowElements.Clear();
            _selectWindowRows.Clear();
        }

        private void AddSelectWindowElement(string labelText, Sprite sprite, EventCallback<ClickEvent> onClick, bool hide = false)
        {
            VisualElement element = selectWindowElementTemplate.Instantiate();
            foreach (var j in element.Children())
            {
                element = j;
                break;
            }

            if (_selectWindowRows.Count < _selectWindowElements.Count / SelectWindowColumnCount + 1)
            {
                // create new visual element
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                _selectWindowScrollView.contentContainer.Add(row);
                _selectWindowRows.Add(row);
            }
            
            element.Q<Label>().text = labelText;
            element.Q<Image>().sprite = sprite;
            _selectWindowElements.Add(element);
            _selectWindowRows[^1].Add(element);
            
            if(onClick != null) element.RegisterCallback(onClick);
            if(hide) element.style.visibility = Visibility.Hidden;
        }

        private void PlayInGame()
        {
            loadingScreenGUI.ShowLoadingPanel(() =>
            {
                SceneTransitionData.levelData = LevelManager.instance.currentLevel;
                SceneManager.LoadSceneAsync("GameScene");
            });
        }

        private void TogglePause()
        {
            if(LevelManager.instance.isPlaying) LevelManager.instance.Pause();
            else LevelManager.instance.Play();
        }

        private void OnClickButton()
        {
            SoundManager.instance.PlaySfx(buttonSoundClip, volume: buttonSoundVolume);
        }

        private void OnFocusOutSnapSizeTextField(FocusOutEvent evt)
        {
            if (float.TryParse(snapSizeTextField.value, out var beat))
            {
                snapSize = beat;
            }
            else if (snapSizeTextField.value.Contains("/") &&
                     int.TryParse(snapSizeTextField.value.Split("/")[0], out var a) &&
                     int.TryParse(snapSizeTextField.value.Split("/")[1], out var b))
            {
                snapSize = (float)a / b;
            }

            switch (snapSize)
            {
                case < 1/32f:
                    snapSize = 1/32f;
                    snapSizeTextField.value = "0.03125";
                    break;
                case > 1f:
                    snapSize = 1f;
                    snapSizeTextField.value = "1";
                    break;
            }
        }

        private void OnFocusOutNoteScrollSpeedTextField(FocusOutEvent evt)
        {
            if (float.TryParse(noteScrollSpeedTextField.value, out var speed))
            {
                LevelManager.instance.currentLevel.baseScrollSpeed = Mathf.Clamp(speed, 1f, 20f);
            }
            else
            {
                noteScrollSpeedTextField.value = $"{LevelManager.instance.currentLevel.baseScrollSpeed :0.0}";
            }
        }

        private void OnFocusOutMeasureTextField(FocusOutEvent evt)
        {
            if (int.TryParse(measureTextField.value, out var measure))
            {
                LevelManager.instance.currentLevel.beatsPerMeasure = Mathf.Clamp(measure, 1, 16);
            }
            else
            {
                measureTextField.value = $"{LevelManager.instance.currentLevel.beatsPerMeasure}";
            }
        }

        public void Zoom(float delta)
        {
            _horizontalGuideSpan += delta;
        }

        public void ToggleSnap()
        {
            _isSnapEnabled = !_isSnapEnabled;
        }

        public void SaveLevelAs()
        {
            StandaloneFileBrowser.SaveFilePanelAsync(
                "저장 경로 선택", 
                Application.dataPath, 
                "New Level.json", 
                "json",
                path =>
                {
                    if (!string.IsNullOrEmpty(path))
                    {
                        LevelManager.instance.SaveLevel(path).Forget();
                    }
                });
        }

        public void OpenLevel()
        {
            StandaloneFileBrowser.OpenFilePanelAsync(
                "레벨 파일 선택", 
                Application.dataPath, 
                "json", 
                false,
                paths =>
                {
                    if(paths.Length > 0) LevelManager.instance.LoadLevel(paths[0]).Forget();
                });
        }

        private void LoadMusicFile()
        {
            
            StandaloneFileBrowser.OpenFilePanelAsync(
                "음악 파일 선택", 
                Application.dataPath, 
                AudioTypeFinder.avaliableExtensions.Select(ext => new ExtensionFilter(ext.ToUpper(), ext)).ToArray(), 
                false,
                paths =>
                {
                    if (paths.Length > 0)
                    {
                        var path = paths[0];
                        if (LevelManager.instance.currentLevel.loadedPath != null)
                        {
                            path = Path.GetRelativePath(LevelManager.instance.currentLevel.loadedPath, path);
                        }
                        LevelManager.instance.currentLevel.musicPath = path;
                        LevelManager.instance.LoadMusic().Forget();
                    }
                });
        }

        public void ToggleAutoPlay()
        {
            LevelManager.instance.player.autoPlay = !LevelManager.instance.player.autoPlay;
        }

        private void OnFocusOutBpmTextField(FocusOutEvent e) 
        {
            if (int.TryParse(bpmTextField.value, out var result))
            {
                if (bpmTextField.value == string.Empty) result = 0;
                LevelManager.instance.currentLevel.defaultBpm = result;
                    
            }
            else
            {
                bpmTextField.value = $"{LevelManager.instance.currentLevel.defaultBpm}";
            }
        }
        
        private void OnFocusOutStartOffsetTextField(FocusOutEvent e)
        {
            if (float.TryParse(startOffsetTextField.value, out var result))
            {
                if (bpmTextField.value == string.Empty) result = 0f;
                LevelManager.instance.currentLevel.startOffset = result;
            }
            else
            {
                startOffsetTextField.value = $"{LevelManager.instance.currentLevel.startOffset}";
            }
        }

        public void SaveLevel()
        {
            var path = LevelManager.instance.currentLevel.loadedPath;
            if (string.IsNullOrEmpty(path))
            {
                SaveLevelAs();
                return;
            }
            LevelManager.instance.SaveLevel(path).Forget();
            ShowToastTask("저장되었습니다.").Forget();
        }

        private void DuplicateElements()
        {
            DuplicateNotes();
            DuplicateEvents();
        }

        private void DuplicateNotes()
        {
            var targets = noteWrappers.Where(w => w.isSelected).ToList();

            var selectTargetNoteObjects = new List<NoteObject>();
            
            foreach (var wrapper in targets)
            {
                var clone = wrapper.noteObject.note.Clone();
                clone.appearBeat += 1f;
                var addedNoteObject = LevelManager.instance.AddPattern(clone);
                
                selectTargetNoteObjects.Add(addedNoteObject);
            }
            
            NoteRenderUpdate();
            DeselectNote(null);
            
            foreach (var wrapper in noteWrappers.Where(wrapper => selectTargetNoteObjects.Contains(wrapper.noteObject)))
            {
                SelectNote(wrapper, true);
            }
        }

        private void DuplicateEvents()
        {
            var targets = eventWrappers.Where(w => w.isSelected).ToList();

            var addedEvents = new List<LevelEvent>();
            foreach (var wrapper in targets)
            {
                var addedEvent = LevelManager.instance.AddEvent(wrapper.levelEvent.Clone());
                
                addedEvents.Add(addedEvent);
                wrapper.isSelected = false;
            }
            
            EventRenderUpdate();
            
            foreach (var wrapper in eventWrappers.Where(wrapper => addedEvents.Contains(wrapper.levelEvent)))
            {
                wrapper.isSelected = true;
            }
        }

        private async UniTask ShowToastTask(string text, float time = 1f)
        {
            toastLabel.text = text;
            toastLabel.style.opacity = new StyleFloat(1f);
            await UniTask.Delay((int)(time * 1000));
            toastLabel.style.opacity = new StyleFloat(0f);
        }

        public void MoveTime(float deltaBeat)
        {
            LevelManager.instance.Seek(LevelManager.instance.currentPlayTime + LevelManager.instance.BeatToPlayTime(deltaBeat));
        }

        private void OnClickMoveLeftBtn()
        {
            var targets = noteWrappers.Where(w => w.isSelected).ToList();
            foreach (var wrapper in targets)
            {
                wrapper.noteObject.note.appearBeat = Mathf.Max(wrapper.noteObject.note.appearBeat - 1f, 0);
            }
        }

        private void OnClickMoveRightBtn()
        {
            var targets = noteWrappers.Where(w => w.isSelected).ToList();
            foreach (var wrapper in targets)
            {
                wrapper.noteObject.note.appearBeat = Mathf.Max(wrapper.noteObject.note.appearBeat + 1f, 0);
            }
        }

        public void DeleteSelectedElements()
        {
            var noteTargets = noteWrappers
                .Where(w => w.isSelected).ToList();
            foreach (var wrapper in noteTargets)
            {
                DeleteNote(wrapper);
            }
            
            var eventTargets = eventWrappers
                .Where(w => w.isSelected).ToList();
            foreach (var wrapper in eventTargets)
            {
                DeleteEvent(wrapper);
            }
        }

        private void OnChangeTime(float x)
        {
            x -= timeChangeZone.layout.xMin;
            
            if (_isCurrentTimeChanging)
            {
                var beat = currentLeftEndBeat + x / _horizontalGuideSpan;

                if (_isSnapEnabled)
                {
                    beat = ToQuantizedBeat(beat);
                }

                LevelManager.instance.Seek(LevelManager.instance.BeatToPlayTime(beat));
            }
        }

        public float ToQuantizedBeat(float beat, bool useSnapSpaceSize = true)
        {
            var m = ExtraMath.PositiveMod(beat, snapSize);
            if (m <= snapSize * (useSnapSpaceSize ? snapSpaceSize : 0.5f)) beat -= m;
            else if (m >= snapSize * (1 - (useSnapSpaceSize ? snapSpaceSize : 0.5f))) beat += snapSize - m;

            return beat;
        }

        private void OnTimelineDragging(float curX)
        {
            currentLeftEndBeat = _timelineDragStartLeftEndBeat - TimelineScaleToBeat(curX - _timelineDragStartX);
        }

        private float TimelineScaleToBeat(float px)
        {
            return px / _horizontalGuideSpan;
        }

        private float BeatToTimelineScale(float beat)
        {
            return beat * _horizontalGuideSpan;
        }

        private void OnClickNote(PointerDownEvent e, NoteElementWrapper wrapper)
        {
            if (e.button == 0)
            {
                SelectNote(wrapper, e.shiftKey);
            }
            else
            {
                DeselectNote(wrapper, e.shiftKey);
            }
        }

        private void OnClickEventElement(PointerDownEvent e, EventElementWrapper wrapper)
        {
            if (e.button == 0)
            {
                SelectEvent(wrapper, e.shiftKey);
            }
            else
            {
                DeselectEvent(wrapper, e.shiftKey);
            }
        }

        public void DeleteNote(NoteElementWrapper wrapper)
        {
            LevelManager.instance.currentLevel.pattern.Remove(wrapper.noteObject.note);
            LevelManager.instance.noteObjects.Remove(wrapper.noteObject);
            noteWrappers.Remove(wrapper);
            wrapper.element.RemoveFromHierarchy();
            Destroy(wrapper.noteObject.gameObject);
        }

        public void DeleteEvent(EventElementWrapper wrapper)
        {
            LevelManager.instance.currentLevel.events.Remove(wrapper.levelEvent);
            eventWrappers.Remove(wrapper);
            wrapper.element.RemoveFromHierarchy();
        }

        public void AddNote()
        {
            var newNote = new Note
            {
                appearBeat = LevelManager.instance.currentBeat,
                noteType = LevelManager.instance.currentBoss.noteMap.Keys.ToList()[0]
            };
            LevelManager.instance.AddPattern(newNote);
            NoteRenderUpdate();
            foreach (var w in noteWrappers)
            {
                if(w.noteObject.note == newNote) SelectNote(w);
            }
        }
        
        public void AddEvent()
        {
            var newEvent = new LevelEvent
            {
                appearBeat = LevelManager.instance.currentBeat
            };
            LevelManager.instance.AddEvent(newEvent);
            EventRenderUpdate();
            foreach (var w in eventWrappers)
            {
                if(w.levelEvent == newEvent) SelectEvent(w);
            }
        }

        public void SelectNote(NoteElementWrapper wrapper, bool isMultiSelect = false)
        {
            if (!isMultiSelect)
            {
                foreach (var w in noteWrappers)
                {
                    w.isSelected = false;
                }
            }
            wrapper.isSelected = true;
            
            SelectMenu(propsMenuSelector, propsMenu);
        }

        public void DeselectNote(NoteElementWrapper wrapper, bool isMultiDeselect = false)
        {
            if (!isMultiDeselect)
            {
                foreach (var w in noteWrappers)
                {
                    w.isSelected = false;
                }
            }
            else wrapper.isSelected = false;
        }

        public void SelectEvent(EventElementWrapper wrapper, bool isMultiSelect = false)
        {
            if (!isMultiSelect)
            {
                foreach (var w in eventWrappers)
                {
                    w.isSelected = false;
                }
            }
            wrapper.isSelected = true;
            
            SelectMenu(propsMenuSelector, propsMenu);
        }

        public void DeselectEvent(EventElementWrapper wrapper, bool isMultiDeselect = false)
        {
            if (!isMultiDeselect)
            {
                foreach (var w in eventWrappers)
                {
                    w.isSelected = false;
                }
            }
            else wrapper.isSelected = false;
        }

        public void SelectMenu(VisualElement selector, VisualElement menu)
        {
            var beforeSelector = _root.Q<VisualElement>(classes: new[] { "menu-selector", "selected" });
            beforeSelector.RemoveFromClassList("selected");
            beforeSelector.AddToClassList("unselected");
            var beforeMenu = _root.Q<VisualElement>(classes: new[] { "side-menu", "selected" });
            beforeMenu.RemoveFromClassList("selected");
            
            selector.AddToClassList("selected");
            selector.RemoveFromClassList("unselected");
            menu.AddToClassList("selected");
        }

        private void OnPointerDownInTimeChangeZone(PointerDownEvent e)
        {
            _isCurrentTimeChanging = true;
            OnChangeTime(e.position.x);
        }

        private void OnPointerDownInTimeline(PointerDownEvent e)
        {
            _isTimelineDragging = true;
            _timelineDragStartX = e.position.x;
            _timelineDragStartLeftEndBeat = currentLeftEndBeat;
        }

        private void OnPointerDownInNoteTrack(PointerDownEvent e)
        {
            if (e.button == 0)
            {
                DeselectEvent(null, false);
                _isNoteTrackDragging = true;
                _noteTrackDragStartPos = e.position;
            }
        }

        private void OnPointerDownInEventTrack(PointerDownEvent e)
        {
            if (e.button == 0)
            {
                DeselectNote(null, false);
                _isEventTrackDragging = true;
                _eventTrackDragStartPos = e.position;
            }
        }

        private void OnPointerMoveInRoot(PointerMoveEvent e)
        {
            if (_isCurrentTimeChanging)
            {
                OnChangeTime(e.position.x);
            }
            else if (_isNoteTrackDragging)
            {
                OnNoteSelectionBoxDragging(e.position);
            }
            else if (_isEventTrackDragging)
            {
                OnEventSelectionBoxDragging(e.position);
            }
            else if (_isTimelineDragging)
            {
                OnTimelineDragging(e.position.x);
            }
        }

        private void OnEventSelectionBoxDragging(Vector3 endPos)
        {
            eventSelectionBox.RemoveFromClassList("hidden");
            var leftTopPos = new Vector2(Mathf.Min(endPos.x, _eventTrackDragStartPos.x),
                Mathf.Min(endPos.y, _eventTrackDragStartPos.y));
            
            eventSelectionBox.style.left = leftTopPos.x - eventTrack.worldBound.xMin;
            eventSelectionBox.style.top = leftTopPos.y - eventTrack.worldBound.yMin;

            var size = (Vector2)endPos - _eventTrackDragStartPos;
            eventSelectionBox.style.width = Mathf.Abs(size.x);
            eventSelectionBox.style.height = Mathf.Abs(size.y);
            
            foreach (var w in eventWrappers)
            {
                var eventPos = w.element.worldBound.center;
                w.isInSelectionBox = eventSelectionBox.worldBound.Contains(eventPos);
            }
        }

        private void OnNoteSelectionBoxDragging(Vector3 endPos)
        {
            noteSelectionBox.RemoveFromClassList("hidden");
            var leftTopPos = new Vector2(Mathf.Min(endPos.x, _noteTrackDragStartPos.x),
                Mathf.Min(endPos.y, _noteTrackDragStartPos.y));
            
            noteSelectionBox.style.left = leftTopPos.x - noteTrack.worldBound.xMin;
            noteSelectionBox.style.top = leftTopPos.y - noteTrack.worldBound.yMin;

            var size = (Vector2)endPos - _noteTrackDragStartPos;
            noteSelectionBox.style.width = Mathf.Abs(size.x);
            noteSelectionBox.style.height = Mathf.Abs(size.y);
            
            foreach (var w in noteWrappers)
            {
                var notePos = w.element.worldBound.center;
                w.isInSelectionBox = noteSelectionBox.worldBound.Contains(notePos);
            }
        }

        private void OnPointerUpInRoot(PointerUpEvent e)
        {
            noteSelectionBox.AddToClassList("hidden");
            eventSelectionBox.AddToClassList("hidden");

            if (!e.shiftKey)
            {
                if(_isNoteTrackDragging && 
                   noteSelectionBox.resolvedStyle.width > 5 && 
                   noteSelectionBox.resolvedStyle.height > 5) DeselectNote(null);
                if(_isEventTrackDragging && 
                   eventSelectionBox.resolvedStyle.width > 5 && 
                   eventSelectionBox.resolvedStyle.height > 5) DeselectEvent(null);
            }
            
            foreach (var w in noteWrappers)
            {
                if (w.isInSelectionBox) SelectNote(w, true);
                w.isInSelectionBox = false;
            }
            foreach (var w in eventWrappers)
            {
                if (w.isInSelectionBox) SelectEvent(w, true);
                w.isInSelectionBox = false;
            }
            
            _isCurrentTimeChanging = false;
            _isTimelineDragging = false;
            _isNoteTrackDragging = false;
            _isEventTrackDragging = false;
        }

        public void CopyElements()
        {
            CopyNotes();
            CopyEvents();
        }

        public void PasteElements()
        {
            if(_copiedNotes.Count > 0) PasteNotes();
            else if(_copiedEvents.Count > 0) PasteEvents();
        }
        
        private void CopyNotes()
        {
            _copiedNotes.Clear();
            var targets = noteWrappers.Where(w => w.isSelected).ToList();

            foreach (var wrapper in targets)
            {
                _copiedNotes.Add(wrapper.noteObject.note.Clone());
            }
        }

        private void PasteNotes()
        {
            if (_copiedNotes == null || _copiedNotes.Count == 0) return;
            
            DeselectNote(null);
            DeselectEvent(null);
            
            var levelManager = LevelManager.instance;
            var currentBeat = levelManager.currentBeat;
            var beatOffset = currentBeat - _copiedNotes[0].appearBeat;
            
            var pastedNotes = _copiedNotes
                .Select(note => 
                {
                    note.appearBeat += beatOffset;
                    return levelManager.AddPattern(note.Clone());
                })
                .ToList();
            
            NoteRenderUpdate();
            
            foreach (var wrapper in noteWrappers)
            {
                if (pastedNotes.Contains(wrapper.noteObject))
                {
                    SelectNote(wrapper, true);
                }
            }
        }

        private void CopyEvents()
        {
            _copiedEvents.Clear();
            var targets = eventWrappers.Where(w => w.isSelected).ToList();

            foreach (var wrapper in targets)
            {
                _copiedEvents.Add(wrapper.levelEvent.Clone());
            }
        }

        private void PasteEvents()
        {
            DeselectNote(null);
            DeselectEvent(null);
            
            var startAppearBeat = _copiedEvents.Count > 0 ? _copiedEvents[0].appearBeat : LevelManager.instance.currentBeat;
            var selectTargetEvents = _copiedEvents.Select(levelEvent =>
            {
                levelEvent.appearBeat = LevelManager.instance.currentBeat +
                                        (levelEvent.appearBeat - startAppearBeat);
                return LevelManager.instance.AddEvent(levelEvent);
            }).ToList();

            EventRenderUpdate();
            
            foreach (var wrapper in eventWrappers.Where(wrapper => selectTargetEvents.Contains(wrapper.levelEvent)))
            {
                SelectEvent(wrapper, true);
            }
            
            CopyEvents();
        }
    }

    public class NoteElementWrapper
    {
        public VisualElement element;
        public NoteObject noteObject;
        public bool isSelected = false;
        public bool isInSelectionBox = false;
    }

    public class EventElementWrapper
    {
        public VisualElement element;
        public LevelEvent levelEvent;
        public bool isSelected = false;
        public bool isInSelectionBox = false;
    }
}
