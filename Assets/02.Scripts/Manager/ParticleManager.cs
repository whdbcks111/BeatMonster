using System.Collections.Generic;
using _02.Scripts.Level;
using _02.Scripts.Level.Note;
using UnityEngine;
using UnityEngine.Serialization;

namespace _02.Scripts.Manager
{
    public class ParticleManager : MonoBehaviour
    {
        public static ParticleManager instance { get; private set; }

        public ParticleSystem hitParticle;
        
        public ParticleSystem defendParticle;
        public ParticleSystem attackParticle;

        private readonly Dictionary<string, (ParticleSystem, ParticleSystem)> _noteSplitParticleMap = new();
        private readonly Dictionary<string, ParticleSystem> _noteParryParticleMap = new();
        
        private void Awake()
        {
            instance = this;
        }

        public void PlayParticle(ParticleSystem origin, Vector3 pos)
        {
            var sprite = origin.textureSheetAnimation.GetSprite(0);
            if (sprite)
            {
                var main = origin.main;
                main.startSize = new ParticleSystem.MinMaxCurve(sprite.rect.width / sprite.pixelsPerUnit);
            }
            
            origin.transform.position = pos;
            origin.Play();
        }
    }
}