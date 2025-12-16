using System;
using _02.Scripts.Manager;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _02.Scripts.Level.Skill
{
    public class SkillBall : MonoBehaviour
    {
        public float duration;
        public float shootTime = 0.5f;

        [NonSerialized] public float awakenBeat;

        private void Awake()
        {
            awakenBeat = LevelManager.instance.currentBeat;
        }

        public void Shoot()
        {
            transform.SetParent(null);
            ShootTask().Forget();
        }

        private async UniTask ShootTask()
        {
            var startPos = transform.position;
            var endPos = LevelManager.instance.currentBoss.transform.position;

            for (var i = 0f; i < 1f; i += Time.deltaTime / shootTime)
            {
                await UniTask.Yield(cancellationToken: destroyCancellationToken);
                transform.position = Vector3.Lerp(startPos, endPos, i);
            }
            
            LevelManager.instance.currentBoss.Hit();
            Destroy(gameObject);
        }
    }
}