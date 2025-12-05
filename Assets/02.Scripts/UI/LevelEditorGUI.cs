using System;
using System.Collections.Generic;
using _02.Scripts.Manager;
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

        [NonSerialized] public Button playBtn, stopBtn, pauseBtn,
            zoomOutBtn, zoomInBtn,
            saveBtn, saveAndExitBtn, exitBtn,
            addNoteBtn, addLastNoteBtn, duplicateBtn, copyBtn,
            delBtn, moveLeftBtn, moveRightBtn;

        [NonSerialized] public Label curTimeLabel, curBeatLabel;

        [NonSerialized] public VisualElement timePicker, 
            noteTrack, eventTrack, 
            timeline, timeLabels, timeChangeZone, timeGuideContainer;

        [NonSerialized] private Image _gameRenderImage;

        private bool _isCurrentTimeChanging = false;
        private bool _isTimelineDragging = false;
        
        private float _horizontalGuideSpan = 100f;
        private float _currentLeftEndBeat = -5f;

        private float _timelineDragStartX, _timelineDragStartLeftEndBeat;

        private readonly List<VisualElement> _guideLines = new();
        
        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            _rootElement = _uiDocument.rootVisualElement;

            BindElements();
            RegisterEvents();
        }

        private void Update()
        {
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

            if (LevelManager.instance && LevelManager.instance.currentLevel != null)
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
            _gameRenderImage = _rootElement.Q<Image>("GameView");
            
            playBtn = _rootElement.Q<Button>("PlayButton");
            stopBtn = _rootElement.Q<Button>("StopButton");
            pauseBtn = _rootElement.Q<Button>("PauseButton");
            
            zoomOutBtn = _rootElement.Q<Button>("ZoomOutButton");
            zoomInBtn = _rootElement.Q<Button>("ZoomInButton");
            
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
        }

        private void RegisterEvents()
        {
            playBtn.clicked += LevelManager.instance.Play;
            pauseBtn.clicked += LevelManager.instance.Pause;
            stopBtn.clicked += LevelManager.instance.Stop;

            zoomInBtn.clicked += () => _horizontalGuideSpan /= 1.1f;
            zoomOutBtn.clicked += () => _horizontalGuideSpan *= 1.1f;
            
            timeChangeZone.RegisterCallback<PointerDownEvent>(OnPointerDownInTimeChangeZone);
            timeline.RegisterCallback<PointerDownEvent>(OnPointerDownInTimeline);
            _rootElement.RegisterCallback<PointerMoveEvent>(OnPointerMoveInRoot);
            _rootElement.RegisterCallback<PointerUpEvent>(OnPointerUpInRoot);
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
}
