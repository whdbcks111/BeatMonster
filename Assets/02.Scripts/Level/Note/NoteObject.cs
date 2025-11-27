using System;
using _02.Scripts.Manager;
using UnityEngine;

namespace _02.Scripts.Level.Note
{
    [RequireComponent(typeof(Collider2D))]
    public class NoteObject : MonoBehaviour
    {
        public string noteType;
        public HitType hitType;
        public AudioClip hitSound;
        public SpriteRenderer spriteRenderer;
        public AnimationCurve moveCurve = new(
            new Keyframe(0f, 0f), 
            new Keyframe(1f, 0f)
            );

        public float scrollSpeedRate = 1f;
        public bool fitInShootPoint = true;
        
        [NonSerialized] public Manager.Note note;

        public bool wasHit = false;
        public bool canPlayHitSound = true;

        private SpriteRenderer _hitPointRenderer;

        private void Awake()
        {
            switch (hitType)
            {
                case HitType.Attack:
                {
                    _hitPointRenderer = Instantiate(LevelManager.instance.attackPoint, transform).GetComponent<SpriteRenderer>();
                    break;
                }
                case HitType.Defend:
                {
                    _hitPointRenderer = Instantiate(LevelManager.instance.defendPoint, transform).GetComponent<SpriteRenderer>();
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
            spriteRenderer.enabled = false;
            _hitPointRenderer.enabled = spriteRenderer.enabled;
        }

        private void Start()
        {
            UpdatePosition();
        }

        protected virtual void UpdatePosition()
        {
            var beatPos = note.appearBeat - LevelManager.instance.currentBeat;
            var playerDist = transform.position.x - LevelManager.instance.player.hitPoint.position.x;
            var totalDist = LevelManager.instance.currentBoss.shootPoint.transform.position.x 
                            - LevelManager.instance.player.hitPoint.transform.position.x;
            var shootPointY = LevelManager.instance.currentBoss.shootPoint.position.y;
            var playerHitPointY = LevelManager.instance.player.hitPoint.position.y;
            
            var x = LevelManager.instance.player.hitPoint.transform.position.x 
                + beatPos * LevelManager.instance.currentLevel.baseScrollSpeed * scrollSpeedRate;
            var y = fitInShootPoint ? 
                Mathf.Lerp(playerHitPointY, shootPointY + Mathf.Sin(beatPos * Mathf.PI * 0.8f) * 1.5f, playerDist / totalDist)
                : 0f;
            
            transform.position = new Vector3(x, y);
        }

        private void Update()
        {
            UpdatePosition();
            CheckNoteAppear();
            CheckHitSound();
        }

        protected virtual void CheckNoteAppear()
        {
            if (transform.position.x <= LevelManager.instance.currentBoss.shootPoint.position.x)
            {
                if (wasHit || spriteRenderer.enabled) return;
                
                spriteRenderer.enabled = true;
                _hitPointRenderer.enabled = spriteRenderer.enabled;
                Appear();
            }
            else if (spriteRenderer.enabled)
            {
                spriteRenderer.enabled = false;
                _hitPointRenderer.enabled = spriteRenderer.enabled;
            }
        }

        protected virtual void Appear()
        {
            
        }

        public virtual void Hit()
        {
            wasHit = true;
            spriteRenderer.enabled = false;
            _hitPointRenderer.enabled = spriteRenderer.enabled;
            switch (hitType)
            {
                case HitType.Attack:
                    //ParticleManager.instance.SpawnHitParticle(this);
                    ParticleManager.instance.PlayParticle(ParticleManager.instance.attackParticle, 
                        LevelManager.instance.player.hitPoint.position);
                    LevelManager.instance.currentBoss.Hit();
                    break;
                case HitType.Defend:
                    //ParticleManager.instance.SpawnSplitParticle(this);
                    ParticleManager.instance.PlayParticle(ParticleManager.instance.defendParticle, 
                        LevelManager.instance.player.hitPoint.position);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            ParticleManager.instance.PlayParticle(ParticleManager.instance.hitParticle, 
                LevelManager.instance.player.hitPoint.position);
            LevelManager.instance.currentLevelPlayerData.AddJudgement(
                LevelManager.instance.currentPlayTime,
                LevelManager.instance.BeatToPlayTime(note.appearBeat)
            );
        }

        private void CheckHitSound()
        {
            var beatOffset = (note.appearBeat - LevelManager.instance.currentBeat) /
                LevelManager.instance.currentLevel.defaultBpm * 60f - LevelManager.instance.offset;
            if (beatOffset < LevelManager.instance.offset)
            {
                if (!canPlayHitSound) return;
                SoundManager.instance.PlaySfxScheduled(hitSound, LevelManager.instance.dspTime + beatOffset);
                canPlayHitSound = false;
            }
            else
            {
                canPlayHitSound = true;
            }
        }
    }

    public enum HitType
    {
        Attack, Defend
    }
}