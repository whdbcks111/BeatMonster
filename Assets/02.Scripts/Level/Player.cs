using System;
using System.Collections.Generic;
using _02.Scripts.Level.Note;
using _02.Scripts.Level.Skill;
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

        [Header("Skill")] 
        [SerializeField] private Transform _skillBallContainer;
        [SerializeField] private SkillBall _skillBallPrefab;

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

            var maxCount = 8;
            var shootTargets = new List<SkillBall>();
            for (int i = 0; i < _skillBallContainer.childCount; i++)
            {
                var skillBall = _skillBallContainer.GetChild(i).gameObject.GetComponent<SkillBall>();
                var span = 0.3f;
                var targetPos = Vector3.right * ((i - (_skillBallContainer.childCount - 1) * 0.5f) * span);

                skillBall.transform.localPosition =
                    Vector3.Lerp(skillBall.transform.localPosition, targetPos, 0.5f);

                if (LevelManager.instance.currentBeat - skillBall.awakenBeat > skillBall.duration || 
                    _skillBallContainer.childCount - shootTargets.Count > maxCount)
                {
                    shootTargets.Add(skillBall);
                }
            }
            foreach (var skillBall in shootTargets)
            {
                skillBall.Shoot();
            }
        }

        private void AddSkillBall()
        {
            var skillBall = Instantiate(_skillBallPrefab, _skillBallContainer);
            skillBall.transform.localPosition = Vector3.zero;
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
                AddSkillBall();
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
                AddSkillBall();
            }
        }
    }
}