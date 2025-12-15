using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using _02.Scripts.Level.Note;
using _02.Scripts.Manager;
using Cysharp.Threading.Tasks;
using SFB;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
// ReSharper disable MemberCanBePrivate.Global

namespace _02.Scripts.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class LevelEditorGUI : MonoBehaviour
    {
        private UIDocument _uiDocument;
        private VisualElement _rootElement;

        public VisualTreeAsset timeGuideTemplate;
        public VisualTreeAsset snapGuideTemplate;
        public VisualTreeAsset noteTemplate;

        [NonSerialized] public Button playBtn, stopBtn, pauseBtn, autoPlayBtn, 
            zoomOutBtn, zoomInBtn,
            openBtn, saveBtn, saveAsBtn, exitBtn,
            addNoteBtn, duplicateBtn, copyBtn,
            delBtn, moveLeftBtn, moveRightBtn,
            menuSnapEnableBtn, snapEnableBtn;

        [NonSerialized] public Label curTimeLabel, curBeatLabel, toastLabel, 
            currentBossNameLabel, currentBgNameLabel, currentGroundNameLabel,
            musicFilePathLabel;

        [NonSerialized] public VisualElement timePicker, 
            noteTrack, eventTrack, 
            timeline, timeLabels, timeChangeZone, timeGuideContainer, snapGuideContainer,
            noteSelectionBox, eventSelectionBox;

        [NonSerialized] private VisualElement _gameViewVisualElement;

        [NonSerialized] public VisualElement levelMenuSelector, propsMenuSelector, editorMenuSelector;
        [NonSerialized] public VisualElement levelMenu, propsMenu, editorMenu;

        [NonSerialized] public DropdownField snapSizeDropdown;

        [NonSerialized] public TextField startOffsetTextField, bpmTextField, snapSizeTextField;

        [SerializeField] private float normalBeatGuidelineHideSpan = 15f, subBeatGuidelineHideSpan = 70f;
        [SerializeField] private float snapSpaceSize = 0.3f;

        private bool _isCurrentTimeChanging = false;
        private bool _isTimelineDragging = false;
        private bool _isNoteTrackDragging = false;
        private bool _isEventTrackDragging = false;
        private bool _isSnapEnabled = false;
        
        private float _horizontalGuideSpan = 100f;
        [NonSerialized] public float currentLeftEndBeat = -5f;
        public float snapSize { get; private set; } = 0.25f;

        private float _timelineDragStartX, _timelineDragStartLeftEndBeat;

        private Vector2 _noteTrackDragStartPos, _eventTrackDragStartPos;

        private readonly List<VisualElement> _timeGuideLines = new();
        private readonly List<VisualElement> _snapGuideLines = new();
        public readonly List<NoteVisualElementWrapper> noteWrappers = new();
        
        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            _rootElement = _uiDocument.rootVisualElement;

            BindElements();
            RegisterEvents().Forget();
        }

        private void Update()
        {
            AdjustCameraToGameView();

            TimeGuidelineRenderUpdate();
            SnapGuidelineRenderUpdate();
            NoteRenderUpdate();

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
                currentBossNameLabel.text = LevelManager.instance.currentLevel.bossId;
            }
            
            if (LevelManager.instance.currentLevel != null)
            {
                var currentPlayTime = LevelManager.instance.currentPlayTime;
                var currentBeat = LevelManager.instance.currentBeat;
                var measure = LevelManager.instance.currentLevel.beatsPerMeasure;
                
                musicFilePathLabel.text = Path.GetFileName(LevelManager.instance.currentLevel.musicPath);
                currentGroundNameLabel.text = LevelManager.instance.currentLevel.groundId;
                currentBgNameLabel.text = LevelManager.instance.currentLevel.backgroundId;
                
                timePicker.style.left =
                    new StyleLength(new Length(
                        (currentBeat - currentLeftEndBeat) * _horizontalGuideSpan, 
                        LengthUnit.Pixel));
                if(curTimeLabel != null) curTimeLabel.text = 
                    $"{(currentPlayTime < 0f ? "-":"")}{(int)Mathf.Abs(currentPlayTime / 60) :D2}:{(int)Mathf.Abs(currentPlayTime % 60) :D2}";
                if(curBeatLabel != null) curBeatLabel.text =
                    $"{Mathf.FloorToInt(currentBeat / measure)} bar {Mathf.FloorToInt(PositiveMod(currentBeat, measure))} beat";
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
                    var wrapper = new NoteVisualElementWrapper { element = noteElement };
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

        private void TimeGuidelineRenderUpdate()
        {
            if (!LevelManager.instance) return;
            var neededGuideLineCount = Mathf.FloorToInt(timeChangeZone.resolvedStyle.width / _horizontalGuideSpan + 2) * 4;
            
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
            
                var firstGuidelineBeat = -PositiveMod(currentLeftEndBeat, 1f);
                for (var i = 0; i < _timeGuideLines.Count; i++)
                {
                    var guideLine = _timeGuideLines[i];
                    var label = guideLine.Q<Label>("TimeLabel");
                    var beat = currentLeftEndBeat + i / 4f;

                    var displayBar = Mathf.FloorToInt(beat / measure);
                    var displayBeat = Mathf.FloorToInt(PositiveMod(beat, measure));
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
                var firstGuidelineBeat = -PositiveMod(currentLeftEndBeat, snapSize);
                for (var i = 0; i < _snapGuideLines.Count; i++)
                {
                    var guideLine = _snapGuideLines[i];
                    
                    guideLine.style.left = BeatToTimelineScale(firstGuidelineBeat) + i * snapSize * _horizontalGuideSpan;
                }
            }
        }

        private float PositiveMod(float x, float m)
        {
            var result = x % m;
            if (result < 0f) result += m;
            return result;
        }

        private void BindElements()
        {
            _gameViewVisualElement = _rootElement.Q<VisualElement>("GameView");
            
            playBtn = _rootElement.Q<Button>("PlayButton");
            stopBtn = _rootElement.Q<Button>("StopButton");
            pauseBtn = _rootElement.Q<Button>("PauseButton");
            autoPlayBtn = _rootElement.Q<Button>("AutoPlayButton");
            
            zoomOutBtn = _rootElement.Q<Button>("ZoomOutButton");
            zoomInBtn = _rootElement.Q<Button>("ZoomInButton");

            openBtn = _rootElement.Q<Button>("OpenButton");
            saveBtn = _rootElement.Q<Button>("SaveButton");
            saveAsBtn = _rootElement.Q<Button>("SaveAsButton");
            exitBtn = _rootElement.Q<Button>("ExitButton");
            
            addNoteBtn = _rootElement.Q<Button>("AddNoteButton");
            duplicateBtn = _rootElement.Q<Button>("DuplicateNoteButton");
            copyBtn = _rootElement.Q<Button>("CopyButton");
            
            delBtn = _rootElement.Q<Button>("DeleteButton");
            moveLeftBtn = _rootElement.Q<Button>("MoveLeftButton");
            moveRightBtn = _rootElement.Q<Button>("MoveRightButton");

            curTimeLabel = _rootElement.Q<Label>("TimeDisplay");
            curBeatLabel = _rootElement.Q<Label>("BeatDisplay");

            timePicker = _rootElement.Q<VisualElement>("TimePicker");
            noteTrack = _rootElement.Q<VisualElement>("NoteTrack");
            eventTrack = _rootElement.Q<VisualElement>("EventTrack");
            timeLabels = _rootElement.Q<VisualElement>("TimeLabels");
            timeline = _rootElement.Q<VisualElement>("Timeline");
            timeChangeZone = _rootElement.Q<VisualElement>("TimeChangeZone");

            timeGuideContainer = _rootElement.Q<VisualElement>("TimeGuideContainer");
            snapGuideContainer = _rootElement.Q<VisualElement>("SnapGuideContainer");

            levelMenuSelector = _rootElement.Q<VisualElement>("LevelMenuSelector");
            propsMenuSelector = _rootElement.Q<VisualElement>("PropsMenuSelector");
            editorMenuSelector = _rootElement.Q<VisualElement>("EditorMenuSelector");

            levelMenu = _rootElement.Q<VisualElement>("LevelMenu");
            propsMenu = _rootElement.Q<VisualElement>("PropsMenu");
            editorMenu = _rootElement.Q<VisualElement>("EditorMenu");

            musicFilePathLabel = _rootElement.Q<Label>("MusicFilePathLabel");
            currentBossNameLabel = _rootElement.Q<Label>("CurrentBossNameLabel");
            currentBgNameLabel = _rootElement.Q<Label>("CurrentBGNameLabel");
            currentGroundNameLabel = _rootElement.Q<Label>("CurrentGroundNameLabel");
            
            startOffsetTextField = _rootElement.Q<TextField>("StartOffsetTextField");
            bpmTextField = _rootElement.Q<TextField>("BPMTextField");

            noteSelectionBox = _rootElement.Q<VisualElement>("NoteSelectionBox");
            eventSelectionBox = _rootElement.Q<VisualElement>("EventSelectionBox");

            toastLabel = _rootElement.Q<Label>("ToastLabel");
            snapEnableBtn = _rootElement.Q<Button>("SnapEnableButton");
            menuSnapEnableBtn = _rootElement.Q<Button>("MenuSnapActiveBtn");

            snapSizeDropdown = _rootElement.Q<DropdownField>("SnapSizeDropdownField");
            snapSizeTextField = _rootElement.Q<TextField>("SnapSizeTextField");
            snapSizeDropdown.value = "1/4";
        }

        private async UniTask RegisterEvents()
        {
            playBtn.clicked += () => LevelManager.instance.Play();
            pauseBtn.clicked += () => LevelManager.instance.Pause();
            stopBtn.clicked += () => LevelManager.instance.Stop();

            zoomInBtn.clicked += () => _horizontalGuideSpan /= 1.1f;
            zoomOutBtn.clicked += () => _horizontalGuideSpan *= 1.1f;

            moveLeftBtn.clicked += OnClickMoveLeftBtn;
            moveRightBtn.clicked += OnClickMoveRightBtn;
            
            delBtn.clicked += DeleteSelectedNotes;
            addNoteBtn.clicked += AddNote;
            duplicateBtn.clicked += OnClickDuplicateBtn;

            saveBtn.clicked += SaveLevel;
            saveAsBtn.clicked += SaveLevelAs;
            openBtn.clicked += OpenLevel;
            
            timeChangeZone.RegisterCallback<PointerDownEvent>(OnPointerDownInTimeChangeZone);
            timeline.RegisterCallback<PointerDownEvent>(OnPointerDownInTimeline);
            _rootElement.RegisterCallback<PointerMoveEvent>(OnPointerMoveInRoot);
            _rootElement.RegisterCallback<PointerUpEvent>(OnPointerUpInRoot);
            
            levelMenuSelector.RegisterCallback<ClickEvent>(_ => SelectMenu(levelMenuSelector, levelMenu));
            editorMenuSelector.RegisterCallback<ClickEvent>(_ => SelectMenu(editorMenuSelector, editorMenu));
            propsMenuSelector.RegisterCallback<ClickEvent>(_ => SelectMenu(propsMenuSelector, propsMenu));
            
            snapEnableBtn.RegisterCallback<ClickEvent>(_ => ToggleSnap());
            menuSnapEnableBtn.RegisterCallback<ClickEvent>(_ => ToggleSnap());

            bpmTextField.RegisterCallback<FocusOutEvent>(OnFocusOutBpmTextField);
            startOffsetTextField.RegisterCallback<FocusOutEvent>(OnFocusOutStartOffsetTextField);
            snapSizeTextField.RegisterCallback<FocusOutEvent>(OnFocusOutSnapSizeTextField);

            autoPlayBtn.clicked += ToggleAutoPlay;
            
            noteTrack.RegisterCallback<PointerDownEvent>(OnPointerDownInNoteTrack);
            eventTrack.RegisterCallback<PointerDownEvent>(OnPointerDownInEventTrack);

            await UniTask.WaitUntil(() => LevelManager.instance != null);
            // Level Manager is not null after this context

            LevelManager.instance.onLoaded += () =>
            {
                startOffsetTextField.value = $"{LevelManager.instance.currentLevel.startOffset}";
                bpmTextField.value = $"{LevelManager.instance.currentLevel.defaultBpm}";
            };
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

        private void OnClickDuplicateBtn()
        {
            var targets = noteWrappers.Where(w => w.isSelected).ToList();

            var selectTargetNoteObjects = new List<NoteObject>();
            
            foreach (var wrapper in targets)
            {
                var clone = wrapper.noteObject.note.Clone();
                clone.appearBeat += 1f;
                var addedNoteObject = LevelManager.instance.AddPattern(clone);
                
                selectTargetNoteObjects.Add(addedNoteObject);
                wrapper.isSelected = false;
            }
            
            NoteRenderUpdate();
            
            foreach (var wrapper in noteWrappers.Where(wrapper => selectTargetNoteObjects.Contains(wrapper.noteObject)))
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

        public void DeleteSelectedNotes()
        {
            var targets = noteWrappers.Where(w => w.isSelected).ToList();
            foreach (var wrapper in targets)
            {
                DeleteNote(wrapper);
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
                    //adjust to snap size * integer value
                    var m = PositiveMod(beat, snapSize);
                    if (m <= snapSize * snapSpaceSize) beat -= m;
                    else if (m >= snapSize * (1 - snapSpaceSize)) beat += snapSize - m;
                }

                LevelManager.instance.Seek(LevelManager.instance.BeatToPlayTime(beat));
            }
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

        private void OnClickNote(PointerDownEvent e, NoteVisualElementWrapper wrapper)
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

        public void DeleteNote(NoteVisualElementWrapper wrapper)
        {
            LevelManager.instance.currentLevel.pattern.Remove(wrapper.noteObject.note);
            LevelManager.instance.noteObjects.Remove(wrapper.noteObject);
            noteWrappers.Remove(wrapper);
            wrapper.element.RemoveFromHierarchy();
            Destroy(wrapper.noteObject.gameObject);
        }

        public void AddNote()
        {
            LevelManager.instance.AddPattern(new Note
            {
                appearBeat = LevelManager.instance.currentBeat,
                noteType = LevelManager.instance.currentBoss.noteMap.Keys.ToList()[0]
            });
        }

        public void SelectNote(NoteVisualElementWrapper wrapper, bool isMultiSelect = false)
        {
            if (!isMultiSelect)
            {
                foreach (var w in noteWrappers)
                {
                    w.isSelected = false;
                }
            }
            wrapper.isSelected = true;
        }

        public void DeselectNote(NoteVisualElementWrapper wrapper, bool isMultiDeselect = false)
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

        public void SelectMenu(VisualElement selector, VisualElement menu)
        {
            var beforeSelector = _rootElement.Q<VisualElement>(classes: new[] { "menu-selector", "selected" });
            beforeSelector.RemoveFromClassList("selected");
            beforeSelector.AddToClassList("unselected");
            var beforeMenu = _rootElement.Q<VisualElement>(classes: new[] { "side-menu", "selected" });
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
                _isNoteTrackDragging = true;
                _noteTrackDragStartPos = e.position;
            }
        }

        private void OnPointerDownInEventTrack(PointerDownEvent e)
        {
            if (e.button == 0)
            {
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
            _isCurrentTimeChanging = false;
            _isTimelineDragging = false;
            _isNoteTrackDragging = false;
            _isEventTrackDragging = false;
            
            noteSelectionBox.AddToClassList("hidden");
            eventSelectionBox.AddToClassList("hidden");
            
            foreach (var w in noteWrappers)
            {
                if (w.isInSelectionBox) w.isSelected = true;
                w.isInSelectionBox = false;
            }
        }

        public void CopyElements()
        {
            throw new NotImplementedException();
        }

        public void PasteElements()
        {
            throw new NotImplementedException();
        }
    }

    public class NoteVisualElementWrapper
    {
        public VisualElement element;
        public NoteObject noteObject;
        public bool isSelected = false;
        public bool isInSelectionBox = false;
    }
}
