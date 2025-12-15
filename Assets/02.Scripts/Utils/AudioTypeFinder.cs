using System.IO;
using UnityEngine;

namespace _02.Scripts.Utils
{
    public static class AudioTypeFinder
    {
        public static AudioType GetAudioTypeFromFileExtension(string filePath)
        {
            var extension = Path.GetExtension(filePath);

            if (string.IsNullOrEmpty(extension)) return AudioType.UNKNOWN;

            switch (extension)
            {
                case ".mp3":
                case ".aac":
                case ".m4a":
                    return AudioType.MPEG;
                case ".ogg":
                    return AudioType.OGGVORBIS;
                case ".wav":
                    return AudioType.WAV;
                case ".aiff":
                case ".aif":
                    return AudioType.AIFF;
                case ".mod":
                    return AudioType.MOD;
                case ".it":
                    return AudioType.IT;
                case ".s3m":
                    return AudioType.S3M;
                case ".xm":
                    return AudioType.XM;
                default:
                    return AudioType.UNKNOWN;
            }
        }
    }
}