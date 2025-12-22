using UnityEngine;

namespace _02.Scripts.ScriptableObjects
{
    [CreateAssetMenu(fileName = "GroundData", menuName = "Data/GroundData", order = 0)]
    public class GroundData : ScriptableObject
    {
        public string groundId;
        public string groundName;
        public Sprite sprite;
    }
}