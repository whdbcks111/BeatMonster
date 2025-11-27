using System;
using System.Collections.Generic;
using _02.Scripts.Level.Note;
using _02.Scripts.Manager;
using UnityEngine;

namespace _02.Scripts.Level
{
    public class Player : MonoBehaviour
    {
        private readonly Queue<NoteObject> _triggeredNotes = new();

        public HeartBeat heartbeat;
        public SpriteRenderer spriteRenderer;
        private Animator _animator;
        
        private static readonly int AnimParry = Animator.StringToHash("Attack");
        private static readonly int AnimDefend = Animator.StringToHash("Defend");

        public bool autoPlay;

        public Transform bodyCenter;
        public Transform hitPoint;

        private void Awake()
        {
            _animator = spriteRenderer.gameObject.GetComponent<Animator>();
        }

        private void Update()
        {
            if (autoPlay && _triggeredNotes.Count > 0 &&
                _triggeredNotes.Peek().note.appearBeat <= LevelManager.instance.currentBeat)
            {
                switch (_triggeredNotes.Peek().hitType)
                {
                    case HitType.Defend:
                        Defend();
                        break;
                    case HitType.Attack:
                        Parry();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            
            if (Input.GetKeyDown(KeyCode.F))
            {
                Parry();   
            }

            if (Input.GetKeyDown(KeyCode.J))
            {
                Defend();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if(LevelManager.instance.isPlaying)
                    LevelManager.instance.Pause();
                else 
                    LevelManager.instance.Resume();
            }
        }

        public void Parry()
        {
            _animator.SetTrigger(AnimParry);
            LevelManager.instance.player.heartbeat.NoticeHit();
            if (_triggeredNotes.Count > 0 && _triggeredNotes.Peek().hitType == HitType.Attack)
            {
                var noteObject = _triggeredNotes.Dequeue();
                noteObject.Hit();
            }
        }

        public void Defend()
        {
            _animator.SetTrigger(AnimDefend);
            LevelManager.instance.player.heartbeat.NoticeHit();
            if (_triggeredNotes.Count > 0 && _triggeredNotes.Peek().hitType == HitType.Defend)
            {
                var noteObject = _triggeredNotes.Dequeue();
                noteObject.Hit();
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!other.TryGetComponent(out NoteObject noteObject) || 
                noteObject.wasHit || _triggeredNotes.Contains(noteObject)) return;
            
            _triggeredNotes.Enqueue(noteObject);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.TryGetComponent(out NoteObject noteObject) && _triggeredNotes.Contains(noteObject))
            {
                _triggeredNotes.Dequeue();
                
            }
        }
    }
}