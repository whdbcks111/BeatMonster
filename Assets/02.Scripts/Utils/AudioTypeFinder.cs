using System.IO;
using UnityEngine;

namespace _02.Scripts.Utils
{
    public static class AudioTypeFinder
    {
        public static readonly string[] avaliableExtensions = {
            "mp3", "aac", "m4a", "ogg", "wav", "aiff", "aif", "mod", "it", "s3m", "xm"
        };
        
        public static AudioType GetAudioTypeFromFileExtension(string filePath)
        {
            var extension = Path.GetExtension(filePath);

            if (string.IsNullOrEmpty(extension)) return AudioType.UNKNOWN;

            return extension.Replace(".", string.Empty) switch
            {
                "mp3" or "aac" or "m4a" => AudioType.MPEG,
                "ogg" => AudioType.OGGVORBIS,
                "wav" => AudioType.WAV,
                "aiff" or "aif" => AudioType.AIFF,
                "mod" => AudioType.MOD,
                "it" => AudioType.IT,
                "s3m" => AudioType.S3M,
                "xm" => AudioType.XM,
                _ => AudioType.UNKNOWN
            };
        }
    }
}