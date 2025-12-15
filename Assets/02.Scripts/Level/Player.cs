using System;
using System.Collections.Generic;
using _02.Scripts.Level.Note;
using _02.Scripts.Manager;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _02.Scripts.Level
{
    public class Player : MonoBehaviour
    {
        public HeartBeat heartbeat;
        public SpriteRenderer spriteRenderer;
        private Animator _animator;
        
        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimDefend = Animator.StringToHash("Defend");

        public bool autoPlay;

        public Transform bodyCenter;
        public Transform hitPoint;

        public float playerOffsetX;

        private void Awake()
        {
            _animator = spriteRenderer.gameObject.GetComponent<Animator>();
        }

        private void Update()
        {
            if (!LevelManager.instance || !LevelManager.instance.isLoaded) return;
            
            var nextNote = LevelManager.instance.GetNextNote();
            
            if (autoPlay && nextNote && nextNote.note.appearBeat <= LevelManager.instance.currentBeat)
            {
                print($"Hit! {nextNote.note.appearBeat} {LevelManager.instance.currentBeat}");
                switch (nextNote.hitType)
                {
                    case HitType.Defend:
                        Defend();
                        break;
                    case HitType.Attack:
                        Attack();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public void Attack()
        {
            if(!LevelManager.instance.isPlaying) return;
            
            var nextNote = LevelManager.instance.GetNextNote();
            
            _animator.SetTrigger(AnimAttack);
            LevelManager.instance.player.heartbeat.NoticeHit();

            if (!nextNote || nextNote.hitType != HitType.Attack) return;
            
            var diff = LevelManager.instance.currentBeat - nextNote.note.appearBeat;
            if (diff is > -1f and < 1f)
            {
                nextNote.Hit();
            }
        }

        public void Defend()
        {
            if(!LevelManager.instance.isPlaying) return;
            
            var nextNote = LevelManager.instance.GetNextNote();
            
            _animator.SetTrigger(AnimDefend);
            LevelManager.instance.player.heartbeat.NoticeHit();

            if (!nextNote || nextNote.hitType != HitType.Defend) return;
            
            var diff = LevelManager.instance.currentBeat - nextNote.note.appearBeat;
            if (diff is > -1f and < 1f)
            {
                nextNote.Hit();
            }
        }
    }
}