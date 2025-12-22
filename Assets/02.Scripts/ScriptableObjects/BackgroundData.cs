using UnityEngine;

namespace _02.Scripts.ScriptableObjects
{
    [CreateAssetMenu(fileName = "BackgroundData", menuName = "Data/BackgroundData", order = 0)]
    public class BackgroundData : ScriptableObject
    {
        public string backgroundId;
        public string backgroundName;
        public Sprite sprite;
    }
}