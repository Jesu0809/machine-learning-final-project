using System;
using System.IO;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// How each night went: for the clown's remarks, for balancing the game with real plays
    /// (tools/summarize_playlog.py) and for the save that follows the player between devices.
    /// It holds how the night went and the kind of device, nothing about the person.
    /// </summary>
    public static class PlayLog
    {
        [Serializable]
        public class Night
        {
            public string date;
            public string brain;
            public int night;
            public string outcome;
            public string how;
            public string room;
            public int roomIndex = -1;
            public string mode;
            public string device;
            public float seconds;
            public float nightLength;
            public int objectivesDone;
            public int objectivesTotal;
            public int heard;
            public int seen;
            public int searches;
            public int senseUses;
            public int senseHits;
            public int ducts;
            public int decisions;
            public float threatBefore;
            public float threatAfter;
        }

        public static string FilePath => Path.Combine(Application.persistentDataPath, "playlog.jsonl");

        public static void Write(Night entry)
        {
            entry.date = DateTime.UtcNow.ToString("o");
            entry.device = Application.platform.ToString();
            SaveGame.AddNight(entry);
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                return;
            }
            try
            {
                File.AppendAllText(FilePath, JsonUtility.ToJson(entry) + "\n");
            }
            catch (Exception problem)
            {
                // A full disk or a locked file must never end somebody's night.
                Debug.LogWarning("Play log not written: " + problem.Message);
            }
        }
    }
}
