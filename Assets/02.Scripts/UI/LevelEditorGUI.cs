using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _02.Scripts.Level.Note;
using _02.Scripts.Manager;
using Cysharp.Threading.Tasks;
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
        public VisualTreeAsset noteTemplate;

        [NonSerialized] public Button playBtn, stopBtn, pauseBtn,
            zoomOutBtn, zoomInBtn,
            autoPlayBtn, saveBtn, saveAndExitBtn, exitBtn,
            addNoteBtn, addLastNoteBtn, duplicateBtn, copyBtn,
            delBtn, moveLeftBtn, moveRightBtn;

        [NonSerialized] public Label curTimeLabel, curBeatLabel;

        [NonSerialized] public VisualElement timePicker, 
            noteTrack, eventTrack, 
            timeline, timeLabels, timeChangeZone, timeGuideContainer;

        [NonSerialized] private VisualElement _gameViewVisualElement;

        [NonSerialized] public VisualElement levelMenuSelector, propsMenuSelector, editorMenuSelector;
        [NonSerialized] public VisualElement levelMenu, propsMenu, editorMenu;

        private bool _isCurrentTimeChanging = false;
        private bool _isTimelineDragging = false;
        
        private float _horizontalGuideSpan = 100f;
        private float _currentLeftEndBeat = -5f;

        private float _timelineDragStartX, _timelineDragStartLeftEndBeat;

        private readonly List<VisualElement> _guideLines = new();
        private readonly List<NoteVisualElementWrapper> _noteWrappers = new();
        
        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            _rootElement = _uiDocument.rootVisualElement;

            BindElements();
            RegisterEvents();
            
        }

        private void Update()
        {
            AdjustCameraToGameView();

            TimelineRenderUpdate();
            NoteRenderUpdate();

            if (LevelManager.instance)
            {
                if (LevelManager.instance.player.autoPlay)
                {
                    autoPlayBtn.AddToClassList("active");
                }
                else
                {
                    autoPlayBtn.RemoveFromClassList("active");
                }
            }
        }

        private void AdjustCameraToGameView()
        {
            if (LevelManager.instance)
            {
                LevelManager.instance.playingViewportStart = new Vector2(
                    0f,
                    1f - _gameViewVisualElement.resolvedStyle.height / Screen.height
                );
                LevelManager.instance.playingViewportEnd = new Vector2(
                    _gameViewVisualElement.resolvedStyle.width / Screen.width,
                    1f
                );
            }
        }

        private void NoteRenderUpdate()
        {
            if (!LevelManager.instance) return;
            var noteObjectCount = LevelManager.instance.noteObjects.Count;

            if (_noteWrappers.Count > noteObjectCount)
            {
                var removeCount = _noteWrappers.Count - noteObjectCount;
                for (var i = 0; i < removeCount; i++)
                {
                    noteTrack.Remove(_noteWrappers[i].element);
                }
                _noteWrappers.RemoveRange(0, removeCount);
            }
            else if (_noteWrappers.Count < noteObjectCount)
            {
                var addCount = noteObjectCount - _noteWrappers.Count;
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
                    _noteWrappers.Add(wrapper);
                    
                    noteElement.RegisterCallback<PointerDownEvent>(e => OnClickNote(e, wrapper));
                }
            }

            for (var i = 0; i < _noteWrappers.Count; i++)
            {
                var noteObj = _noteWrappers[i].noteObject = LevelManager.instance.noteObjects[i];
                var element = _noteWrappers[i].element;

                element.style.backgroundImage = noteObj.spriteRenderer.sprite.texture;
                element.style.left = (noteObj.note.appearBeat - _currentLeftEndBeat) * _horizontalGuideSpan;

                if (_noteWrappers[i].isSelected)
                {
                    element.AddToClassList("selected");
                }
                else
                {
                    element.RemoveFromClassList("selected");
                }
            }
        }

        private void TimelineRenderUpdate()
        {
            if (!LevelManager.instance) return;
            var neededGuideLineCount = Mathf.FloorToInt(timeChangeZone.resolvedStyle.width / _horizontalGuideSpan + 2);
            
            if (_guideLines.Count > neededGuideLineCount && neededGuideLineCount >= 0)
            {
                var removeCount = _guideLines.Count - neededGuideLineCount;
                for (var i = 0; i < removeCount; i++)
                {
                    timeGuideContainer.Remove(_guideLines[i]);
                }
                _guideLines.RemoveRange(0, removeCount);
            }
            else if (_guideLines.Count < neededGuideLineCount)
            {
                var addCount = neededGuideLineCount - _guideLines.Count;
                for (var i = 0; i < addCount; i++)
                {
                    var guideLine = timeGuideTemplate.Instantiate() as VisualElement;
                    foreach (var j in guideLine.Children())
                    {
                        guideLine = j;
                        break;
                    }
                    timeGuideContainer.Add(guideLine);
                    _guideLines.Add(guideLine);
                }
            }

            if (LevelManager.instance.currentLevel != null)
            {
                var currentPlayTime = LevelManager.instance.currentPlayTime;
                var currentBeat = LevelManager.instance.currentBeat;
                var measure = LevelManager.instance.currentLevel.beatsPerMeasure;
            
                var firstGuidelineBeat = -PositiveMod(_currentLeftEndBeat, 1f);
                for (var i = 0; i < _guideLines.Count; i++)
                {
                    var guideLine = _guideLines[i];
                    var label = guideLine.Q<Label>("TimeLabel");
                    var beat = _currentLeftEndBeat + i;

                    var displayBar = Mathf.FloorToInt(beat / measure);
                    var displayBeat = Mathf.FloorToInt(PositiveMod(beat, measure));
                    guideLine.style.left = BeatToTimelineScale(firstGuidelineBeat) + i * _horizontalGuideSpan;
                    label.text = $"{displayBar}.{displayBeat}";

                    if (displayBeat == 0)
                    {
                        guideLine.AddToClassList("on-beat");
                        guideLine.RemoveFromClassList("hidden");
                    }
                    else
                    {
                        if(_horizontalGuideSpan < 50f) guideLine.AddToClassList("hidden");
                        else guideLine.RemoveFromClassList("hidden");
                        guideLine.RemoveFromClassList("on-beat");
                    }
                }
            
                timePicker.style.left =
                    new StyleLength(new Length(
                        (currentBeat - _currentLeftEndBeat) * _horizontalGuideSpan, 
                        LengthUnit.Pixel));
                if(curTimeLabel != null) curTimeLabel.text = 
                    $"{(currentPlayTime < 0f ? "-":"")}{(int)Mathf.Abs(currentPlayTime / 60) :D2}:{(int)Mathf.Abs(currentPlayTime % 60) :D2}";
                if(curBeatLabel != null) curBeatLabel.text =
                    $"{Mathf.FloorToInt(currentBeat / measure)} bar {Mathf.FloorToInt(PositiveMod(currentBeat, measure))} beat";
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
            
            zoomOutBtn = _rootElement.Q<Button>("ZoomOutButton");
            zoomInBtn = _rootElement.Q<Button>("ZoomInButton");

            autoPlayBtn = _rootElement.Q<Button>("AutoPlayButton");
            saveBtn = _rootElement.Q<Button>("SaveButton");
            saveAndExitBtn = _rootElement.Q<Button>("SaveAndExitButton");
            exitBtn = _rootElement.Q<Button>("ExitButton");
            
            addNoteBtn = _rootElement.Q<Button>("AddNoteButton");
            addLastNoteBtn = _rootElement.Q<Button>("AddLastNoteButton");
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

            levelMenuSelector = _rootElement.Q<VisualElement>("LevelMenuSelector");
            propsMenuSelector = _rootElement.Q<VisualElement>("PropsMenuSelector");
            editorMenuSelector = _rootElement.Q<VisualElement>("EditorMenuSelector");

            levelMenu = _rootElement.Q<VisualElement>("LevelMenu");
            propsMenu = _rootElement.Q<VisualElement>("PropsMenu");
            editorMenu = _rootElement.Q<VisualElement>("EditorMenu");
        }

        private void RegisterEvents()
        {
            playBtn.clicked += () => LevelManager.instance.Play();
            pauseBtn.clicked += () => LevelManager.instance.Pause();
            stopBtn.clicked += () => LevelManager.instance.Stop();

            zoomInBtn.clicked += () => _horizontalGuideSpan /= 1.1f;
            zoomOutBtn.clicked += () => _horizontalGuideSpan *= 1.1f;

            delBtn.clicked += OnClickDeleteBtn;
            addNoteBtn.clicked += () => AddNote(LevelManager.instance.currentBeat);

            saveBtn.clicked += () => LevelManager.instance.SaveLevel(Path.Combine(Application.dataPath, "test.json")).Forget();
            
            timeChangeZone.RegisterCallback<PointerDownEvent>(OnPointerDownInTimeChangeZone);
            timeline.RegisterCallback<PointerDownEvent>(OnPointerDownInTimeline);
            _rootElement.RegisterCallback<PointerMoveEvent>(OnPointerMoveInRoot);
            _rootElement.RegisterCallback<PointerUpEvent>(OnPointerUpInRoot);
            
            levelMenuSelector.RegisterCallback<ClickEvent>(_ => SelectMenu(levelMenuSelector, levelMenu));
            editorMenuSelector.RegisterCallback<ClickEvent>(_ => SelectMenu(editorMenuSelector, editorMenu));
            propsMenuSelector.RegisterCallback<ClickEvent>(_ => SelectMenu(propsMenuSelector, propsMenu));

            autoPlayBtn.clicked += () => LevelManager.instance.player.autoPlay = !LevelManager.instance.player.autoPlay;
        }

        private void OnClickDeleteBtn()
        {
            var targets = _noteWrappers.Where(w => w.isSelected).ToList();
            foreach (var wrapper in targets)
            {
                DeleteNote(wrapper);
            }
        }

        public NoteVisualElementWrapper GetSelectedNoteElementWrapper()
        {
            return _noteWrappers.FirstOrDefault(wrapper => wrapper.isSelected);
        }

        private void OnChangeTime(float x)
        {
            x -= timeChangeZone.layout.xMin;
            
            if (_isCurrentTimeChanging)
            {
                LevelManager.instance.Seek(LevelManager.instance.BeatToPlayTime(_currentLeftEndBeat + x / _horizontalGuideSpan));
            }
        }

        private void OnTimelineDragging(float curX)
        {
            _currentLeftEndBeat = _timelineDragStartLeftEndBeat - TimelineScaleToBeat(curX - _timelineDragStartX);
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

        private void DeleteNote(NoteVisualElementWrapper wrapper)
        {
            LevelManager.instance.currentLevel.pattern.Remove(wrapper.noteObject.note);
            LevelManager.instance.noteObjects.Remove(wrapper.noteObject);
            _noteWrappers.Remove(wrapper);
            wrapper.element.RemoveFromHierarchy();
            Destroy(wrapper.noteObject.gameObject);
        }

        private void AddNote(float appearBeat)
        {
            LevelManager.instance.AddPattern(new Note
            {
                appearBeat = appearBeat,
                noteType = LevelManager.instance.currentBoss.noteMap.Keys.ToList()[0]
            });
        }

        private void SelectNote(NoteVisualElementWrapper wrapper, bool isMultiSelect = false)
        {
            if (!isMultiSelect)
            {
                foreach (var w in _noteWrappers)
                {
                    w.isSelected = false;
                }
            }
            wrapper.isSelected = true;
        }

        private void DeselectNote(NoteVisualElementWrapper wrapper, bool isMultiDeselect = false)
        {if (!isMultiDeselect)
            {
                foreach (var w in _noteWrappers)
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
            _timelineDragStartLeftEndBeat = _currentLeftEndBeat;
        }

        private void OnPointerMoveInRoot(PointerMoveEvent e)
        {
            if (_isCurrentTimeChanging)
            {
                OnChangeTime(e.position.x);
            }
            else if (_isTimelineDragging)
            {
                OnTimelineDragging(e.position.x);
            }
        }

        private void OnPointerUpInRoot(PointerUpEvent e)
        {
            _isCurrentTimeChanging = false;
            _isTimelineDragging = false;
        }
    }

    public class NoteVisualElementWrapper
    {
        public VisualElement element;
        public NoteObject noteObject;
        public bool isSelected = false;
    }
}
