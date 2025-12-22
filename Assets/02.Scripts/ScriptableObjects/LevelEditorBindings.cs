using _02.Scripts.Manager;
using UnityEngine;

namespace _09.ScriptableObjects
{
    [CreateAssetMenu(fileName = "LevelEditorBindings", menuName = "Binding/LevelEditor", order = 0)]
    public class LevelEditorBindings : ScriptableObject
    {
        public Level level;
    }
}