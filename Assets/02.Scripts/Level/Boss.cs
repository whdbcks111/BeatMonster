using System;
using System.Collections.Generic;
using System.Linq;
using _02.Scripts.Level.Note;
using _02.Scripts.Manager;
using UnityEngine;

namespace _02.Scripts.Level
{
    public class Boss : MonoBehaviour
    {
        private const float HitBloomTime = 0.5f;
        private static readonly int Intensity = Shader.PropertyToID("_Intensity");
        
        public string bossId;
        public string bossName;
        public Sprite displaySprite;
        public Sprite deadSprite;
        [NonSerialized] public float hp, maxHp;
        public readonly Dictionary<string, NoteObject> noteMap = new();
        
        
        [SerializeField] private NoteObject[] notes;
        public Transform shootPoint;
        public Transform bodyCenter;
        
        private SpriteRenderer _spriteRenderer;
        private float _hitBloomTimer = 0f;

        private Sprite _originalSprite;
        

        private void OnDrawGizmos()
        {
            if(!shootPoint) return;

            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(shootPoint.position, 0.1f);

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(bodyCenter.position, 0.1f);
        }

        public void InitHp(float v)
        {
            hp = v;
            maxHp = v;
        }

        private void Awake()
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            _originalSprite = _spriteRenderer.sprite;
            InitNoteMap();
        }

        private void Update()
        {
            if (_hitBloomTimer > 0f)
            {
                _hitBloomTimer -= Time.deltaTime / HitBloomTime;
                _spriteRenderer.material.SetFloat(Intensity, Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(_hitBloomTimer), 1.5f)));
            }
            
            _spriteRenderer.sprite = hp <= 0f ? deadSprite : _originalSprite;
        }

        public void InitNoteMap()
        {
            foreach (var noteObject in notes)
            {
                noteMap[noteObject.noteType] = noteObject;
            }
        }

        public void Hit()
        {
            hp--;
            if (LevelManager.instance.noteObjects.All(no => no.wasHit) &&
                LevelManager.instance.player.currentSkillBallCount <= 0) hp = 0;
            else hp = Mathf.Max(1, hp);
            
            _hitBloomTimer = 1f;
        }
    }
}