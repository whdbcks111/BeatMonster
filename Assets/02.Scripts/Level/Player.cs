using System;
using System.Collections.Generic;
using _02.Scripts.Level.Note;
using _02.Scripts.Level.Skill;
using _02.Scripts.Manager;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace _02.Scripts.Level
{
    public class Player : MonoBehaviour
    {
        public HeartBeat heartbeat;
        public SpriteRenderer spriteRenderer;
        private Animator _animator;
        
        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimDefend = Animator.StringToHash("Defend");
        private static readonly int Intensity = Shader.PropertyToID("_Intensity");

        public bool autoPlay;

        public Transform bodyCenter;
        public Transform hitPoint;

        public int currentSkillBallCount => skillBallContainer.childCount + shotSkillBallContainer.childCount;

        [Header("Skill")] 
        [SerializeField] private Transform skillBallContainer, shotSkillBallContainer;
        [SerializeField] private SkillBall skillBallPrefab;

        [Header("SFX")] 
        [SerializeField] private AudioSource hitAudioSource;
        [SerializeField] private float hitSoundVolume = 1f;

        private float _prepareEndBeat = float.NaN;

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

            const int maxCount = 8;
            var shootTargets = new List<SkillBall>();
            for (var i = 0; i < skillBallContainer.childCount; i++)
            {
                var skillBall = skillBallContainer.GetChild(i).gameObject.GetComponent<SkillBall>();
                var span = 0.3f;
                var targetPos = Vector3.right * ((i - (skillBallContainer.childCount - 1) * 0.5f) * span);

                skillBall.transform.localPosition =
                    Vector3.Lerp(skillBall.transform.localPosition, targetPos, 0.5f);

                if (LevelManager.instance.currentBeat - skillBall.awakenBeat > skillBall.duration || 
                    skillBallContainer.childCount - shootTargets.Count > maxCount)
                {
                    shootTargets.Add(skillBall);
                }
            }
            foreach (var skillBall in shootTargets)
            {
                skillBall.Shoot(shotSkillBallContainer);
            }

            if (LevelManager.instance.currentBeat < _prepareEndBeat && !float.IsNaN(_prepareEndBeat))
            {
                spriteRenderer.material.SetFloat(Intensity, (Mathf.Sin(Mathf.PI * 2 * LevelManager.instance.currentBeat) + 1) / 2f);
            }
            else
            {
                spriteRenderer.material.SetFloat(Intensity, 0f);
            }
        }

        private void AddSkillBall()
        {
            var skillBall = Instantiate(skillBallPrefab, skillBallContainer);
            skillBall.transform.localPosition = Vector3.zero;
        }

        public void ResetSkillBall()
        {
            for (int i = 0; i < skillBallContainer.childCount; i++)
            {
                var skillBallObj = skillBallContainer.GetChild(i).gameObject;
                Destroy(skillBallObj);
            }
            for (int i = 0; i < shotSkillBallContainer.childCount; i++)
            {
                var skillBallObj = shotSkillBallContainer.GetChild(i).gameObject;
                Destroy(skillBallObj);
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

        public void SetPrepareVfx(float endBeat)
        {
            _prepareEndBeat = endBeat;
        }

        public void Hit()
        {
            hitAudioSource.PlayOneShot(hitAudioSource.clip, hitSoundVolume);
        }
    }
}