using System;
using _02.Scripts.Manager;
using UnityEngine;

namespace _02.Scripts.Level
{
    public class HeartBeat : MonoBehaviour
    {
        private Vector3 _initScale;
        private float _hitBeatTime = float.NaN;
        public float maxBeatScale = 1.4f;
        public float minBeatScale = 0.3f;

        public Vector3 weight = Vector3.one;
    
        private void Start()
        {
            _initScale = transform.localScale;
        }

        public void NoticeHit()
        {
            _hitBeatTime = LevelManager.instance.currentBeat;
        }

        private void Update()
        {
            if (!LevelManager.instance) return;
            
            var m = LevelManager.instance.currentBeat % 1.0f;
            if (m < 0) m = 1 - m;

            if (!float.IsNaN(_hitBeatTime) && LevelManager.instance.currentBeat - _hitBeatTime is <= 1f and > 0f)
            {
                m = LevelManager.instance.currentBeat - _hitBeatTime;
            }
                
            transform.localScale = _initScale * Mathf.Lerp(
                minBeatScale, maxBeatScale, 
                Mathf.Pow(1 - m, 2f)
            );

            transform.localScale = new Vector3(
                Mathf.Lerp(_initScale.x, transform.localScale.x, weight.x),
                Mathf.Lerp(_initScale.y, transform.localScale.y, weight.y),
                Mathf.Lerp(_initScale.z, transform.localScale.z, weight.z)
            );
        }
    }
}
