using System;
using System.Collections.Generic;
using _02.Scripts.Level.Note;
using UnityEngine;

namespace _02.Scripts.Level
{
    public class Boss : MonoBehaviour
    {
        [NonSerialized] public Action onHit = () => { };
        [NonSerialized] public int hp, maxHp;
        public readonly Dictionary<string, NoteObject> noteMap = new();
        
        
        [SerializeField] private NoteObject[] notes;
        public Transform shootPoint;

        private void OnDrawGizmos()
        {
            if(!shootPoint) return;

            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(shootPoint.position, 0.1f);
        }

        public void InitHp(int v)
        {
            hp = v;
            maxHp = v;
        }

        private void Awake()
        {
            foreach (var noteObject in notes)
            {
                noteMap[noteObject.noteType] = noteObject;
            }

            transform.position = new Vector3(
                Camera.main.ViewportToWorldPoint(new Vector3(1, 0)).x,
                0
                );
        }

        public void Hit()
        {
            hp--;
            onHit.Invoke();
        }
    }
}