using System;
using _02.Scripts.Level;
using Newtonsoft.Json;
using UnityEngine;

namespace _02.Scripts.ScriptableObjects
{
    [CreateAssetMenu(fileName = "World", menuName = "Data/World", order = 0)]
    public class WorldData : ScriptableObject
    {
        public string worldName;
        public Sprite worldBackground;
        public Sprite worldGround;
        
        public StageData[] stages;
    }

    [Serializable]
    public class StageData
    {
        public Boss boss;
        public TextAsset levelFileAsset;
        
        [NonSerialized] private Manager.Level _levelDataCache = null;
        public Manager.Level levelData => _levelDataCache ??= JsonConvert.DeserializeObject<Manager.Level>(levelFileAsset.text);
    }
}