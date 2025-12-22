using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace _02.Scripts.Settings
{
    public static class GameSettings
    {
        public const string SettingsFileName = "settings.json";
        
        public static SettingData settings {
            get
            {
                Initialize();
                return _settingsCache;
            }

            set
            {
                _settingsCache = value;
                var path = Path.Combine(Application.persistentDataPath, SettingsFileName);
                File.WriteAllText(path, JsonConvert.SerializeObject(_settingsCache));
            }
        }

        public static void Initialize()
        {
            if(_initialized) return;
            
            var path = Path.Combine(Application.persistentDataPath, SettingsFileName);
            _settingsCache = File.Exists(path) 
                ? JsonConvert.DeserializeObject<SettingData>(File.ReadAllText(path)) 
                : new SettingData();
            _initialized = true;
        }

        private static bool _initialized = false;
        private static SettingData _settingsCache;
    }

    [Serializable]
    public struct SettingData
    {
        public float audioOffset;
    }
}