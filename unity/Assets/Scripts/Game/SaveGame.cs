using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>Everything the game remembers about a player, the same on every device.</summary>
    [Serializable]
    public class SaveData
    {
        public long updatedAt;
        public int bestNight = 1;
        public float threat = Hunt.ThreatLevel.Start;
        public bool finishedStory;
        public int encoreBest;
        public string dailyDate = "";
        public float dailyBest;
        public int hidLocker;
        public int hidCabinet;
        public int hidTable;
        public List<PlayLog.Night> history = new List<PlayLog.Night>();
    }

    /// <summary>
    /// The save on this device. Nobody has to do anything: it loads by itself and writes after every
    /// night, and <see cref="CloudSave"/> keeps it in step with the other devices of the player.
    /// </summary>
    public static class SaveGame
    {
        /// <summary>Nights kept in the history; the oldest go first.</summary>
        private const int HistoryLimit = 200;

        private const string WebKey = "NightShift.Save";

        private static SaveData data;

        public static SaveData Data => data ?? (data = Load());

        private static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

        /// <summary>Called after anything changes.</summary>
        public static event Action Changed;

        public static void Save()
        {
            // Recording a film or taking pictures must not touch the player's progress.
            if (CaptureMode.Active)
            {
                return;
            }
            Data.updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Write();
            Changed?.Invoke();
        }

        public static void AddNight(PlayLog.Night night)
        {
            Data.history.Add(night);
            if (Data.history.Count > HistoryLimit)
            {
                Data.history.RemoveRange(0, Data.history.Count - HistoryLimit);
            }
            Save();
        }

        /// <summary>
        /// Joins what another device saved: the best of the progress, the nights of both, and the
        /// threat level of whichever device played last.
        /// </summary>
        public static void Merge(SaveData other)
        {
            if (other == null)
            {
                return;
            }
            Data.bestNight = Mathf.Max(Data.bestNight, other.bestNight);
            Data.finishedStory |= other.finishedStory;
            Data.encoreBest = Mathf.Max(Data.encoreBest, other.encoreBest);
            Data.hidLocker = Mathf.Max(Data.hidLocker, other.hidLocker);
            Data.hidCabinet = Mathf.Max(Data.hidCabinet, other.hidCabinet);
            Data.hidTable = Mathf.Max(Data.hidTable, other.hidTable);
            if (string.CompareOrdinal(other.dailyDate, Data.dailyDate) > 0)
            {
                Data.dailyDate = other.dailyDate;
                Data.dailyBest = other.dailyBest;
            }
            else if (other.dailyDate == Data.dailyDate && other.dailyBest > 0f)
            {
                Data.dailyBest = Data.dailyBest > 0f ? Mathf.Min(Data.dailyBest, other.dailyBest) : other.dailyBest;
            }
            var known = new HashSet<string>();
            foreach (PlayLog.Night night in Data.history)
            {
                known.Add(night.date + night.night);
            }
            foreach (PlayLog.Night night in other.history)
            {
                if (known.Add(night.date + night.night))
                {
                    Data.history.Add(night);
                }
            }
            Data.history.Sort((a, b) => string.CompareOrdinal(a.date, b.date));
            if (Data.history.Count > HistoryLimit)
            {
                Data.history.RemoveRange(0, Data.history.Count - HistoryLimit);
            }
            if (other.updatedAt > Data.updatedAt)
            {
                Data.threat = other.threat;
            }
            Data.updatedAt = Math.Max(Data.updatedAt, other.updatedAt);
            Write();
        }

        private static SaveData Load()
        {
            SaveData loaded = null;
            try
            {
                string json = Application.platform == RuntimePlatform.WebGLPlayer
                    ? PlayerPrefs.GetString(WebKey, "")
                    : File.Exists(FilePath) ? File.ReadAllText(FilePath) : "";
                if (json.Length > 0)
                {
                    loaded = JsonUtility.FromJson<SaveData>(json);
                }
            }
            catch (Exception problem)
            {
                Debug.LogWarning("Save not read: " + problem.Message);
            }
            if (loaded == null)
            {
                // Progress from before the save existed.
                loaded = new SaveData
                {
                    bestNight = PlayerPrefs.GetInt("NightShift.BestNight", 1),
                    threat = PlayerPrefs.GetFloat("NightShift.Threat", Hunt.ThreatLevel.Start)
                };
            }
            if (loaded.history == null)
            {
                loaded.history = new List<PlayLog.Night>();
            }
            return loaded;
        }

        private static void Write()
        {
            try
            {
                string json = JsonUtility.ToJson(Data);
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    PlayerPrefs.SetString(WebKey, json);
                    PlayerPrefs.Save();
                }
                else
                {
                    File.WriteAllText(FilePath, json);
                }
            }
            catch (Exception problem)
            {
                // A full disk or a locked file must never end somebody's night.
                Debug.LogWarning("Save not written: " + problem.Message);
            }
        }
    }
}
