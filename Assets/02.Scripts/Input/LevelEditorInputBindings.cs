using System;
using System.Linq;
using _02.Scripts.Manager;
using _02.Scripts.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _02.Scripts.Input
{
    [RequireComponent(typeof(PlayerInput))]
    public class LevelEditorInputBindings : MonoBehaviour
    {
        [SerializeField] private LevelEditorGUI editor;

        private void OnTimeMoveLeft(InputValue value)
        {
            editor.MoveTime(-1f);
        }
        
        private void OnTimeMoveRight(InputValue value)
        {
            editor.MoveTime(1f);
        }
        
        private void OnTimeMoveSnapLeft(InputValue value)
        {
            editor.MoveTime(-editor.snapSize);
        }
        
        private void OnTimeMoveSnapRight(InputValue value)
        {
            editor.MoveTime(editor.snapSize);
        }
        
        private void OnZoom(InputValue value)
        {
            editor.Zoom(value.Get<float>() * 0.1f);
        }
        
        private void OnGoToHome(InputValue value)
        {
            editor.currentLeftEndBeat = 0f;
        }
        
        private void OnGoToEnd(InputValue value)
        {
            editor.currentLeftEndBeat = LevelManager.instance.currentLevel.pattern.OrderBy(note => note.appearBeat).FirstOrDefault()?.appearBeat ?? 0f;
        }
        
        private void OnDelete(InputValue value)
        {
            editor.DeleteSelectedNotes();
        }
        
        private void OnSaveLevel(InputValue value)
        {
            editor.SaveLevel();
        }
        
        private void OnSaveAs(InputValue value)
        {
            editor.SaveLevelAs();
        }
        
        private void OnEscape(InputValue value)
        {
            
        }
        
        private void OnOpenLevel(InputValue value)
        {
            editor.OpenLevel();
        }
        
        private void OnTogglePlayAndPause(InputValue value)
        {
            if (LevelManager.instance.isPlaying)
            {
                LevelManager.instance.Pause();
            }
            else
            {
                LevelManager.instance.Play();
            }
        }
        
        private void OnQuantize(InputValue value)
        {
            
        }
        
        private void OnToggleSnap(InputValue value)
        {
            editor.ToggleSnap();
        }
        
        private void OnDeselect(InputValue value)
        {
            foreach (var wrapper in editor.noteWrappers)
            {
                editor.DeselectNote(wrapper);
            }
        }
        
        private void OnToggleAutoPlay(InputValue value)
        {
            editor.ToggleAutoPlay();
        }
        
        private void OnTimelineScroll(InputValue value)
        {
            editor.currentLeftEndBeat += -value.Get<float>() * 0.25f;
        }
        
        private void OnCopy(InputValue value)
        {
            editor.CopyElements();
        }
        
        private void OnPaste(InputValue value)
        {
            editor.PasteElements();
        }
        
        private void OnSelectAll(InputValue value)
        {
            foreach (var wrapper in editor.noteWrappers)
            {
                editor.SelectNote(wrapper, true);
            }
        }
        
        private void OnSelectNextNote(InputValue value)
        {
            if (editor.noteWrappers.Count == 0) return;
            
            var selectedNotes = editor.noteWrappers.FindAll(w => w.isSelected);
            var selectedMaxBeat = selectedNotes.Max(w => w.noteObject.note.appearBeat);
            var nextNote = selectedNotes.Find(w => w.noteObject.note.appearBeat > selectedMaxBeat);
            
            if(nextNote == null)
            {
                var minBeat = editor.noteWrappers.Min(w => w.noteObject.note.appearBeat);
                editor.SelectNote(editor.noteWrappers.Find(w => w.noteObject.note.appearBeat <= minBeat));
            }
            else
            {
                editor.SelectNote(nextNote);
            }
        }

        private void OnAddNote(InputValue value)
        {
            editor.AddNote();
        }
    }
}