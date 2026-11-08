using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace NightShift.Game
{
    /// <summary>
    /// Keeps the save on the server, by itself and without blocking anything: if there is no internet
    /// the game plays exactly the same and tries again later. The player is an anonymous random id
    /// made on first run. Inside Steam, <see cref="PlayerId"/> is the place to use the Steam account
    /// (or Steam Cloud on the save file), which makes every device automatic.
    /// </summary>
    public class CloudSave : MonoBehaviour
    {
        /// <summary>The deployed site. The web build talks to whatever site it was loaded from.</summary>
        public const string DesktopServer = "https://night-shift.onrender.com";

        /// <summary>Off until the database exists (docs/setup.md, section 9): the save stays on this device.</summary>
        public const bool Enabled = false;

        private const string IdKey = "NightShift.PlayerId";

        private bool pushing;
        private bool pushAgain;

        /// <summary>Called when another device's progress arrived.</summary>
        public event Action Pulled;

        private static string Server
        {
            get
            {
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    var page = new Uri(Application.absoluteURL);
                    return page.GetLeftPart(UriPartial.Authority);
                }
                return DesktopServer;
            }
        }

        public static string PlayerId
        {
            get
            {
                string id = PlayerPrefs.GetString(IdKey, "");
                if (id.Length == 0)
                {
                    id = Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString(IdKey, id);
                    PlayerPrefs.Save();
                }
                return id;
            }
            private set
            {
                PlayerPrefs.SetString(IdKey, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>The web version with this player in its address: opening it continues right there.</summary>
        public static string WebLink => DesktopServer + "/play?p=" + PlayerId;

        public static CloudSave Create(GameObject owner)
        {
            var cloud = owner.AddComponent<CloudSave>();
            cloud.AdoptPlayerFromAddress();
            SaveGame.Changed += cloud.Push;
            cloud.StartCoroutine(cloud.Sync());
            return cloud;
        }

        private void OnDestroy() => SaveGame.Changed -= Push;

        private void AdoptPlayerFromAddress()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                return;
            }
            const string marker = "p=";
            string query = new Uri(Application.absoluteURL).Query.TrimStart('?');
            foreach (string part in query.Split('&'))
            {
                if (part.StartsWith(marker) && IsId(part.Substring(marker.Length)))
                {
                    PlayerId = part.Substring(marker.Length);
                }
            }
        }

        private static bool IsId(string value)
        {
            if (value.Length != 32)
            {
                return false;
            }
            foreach (char c in value)
            {
                if (!Uri.IsHexDigit(c))
                {
                    return false;
                }
            }
            return true;
        }

        public void Push()
        {
            if (pushing)
            {
                pushAgain = true;
                return;
            }
            StartCoroutine(Upload());
        }

        /// <summary>Asks for a short code that lets another device become this player (ten minutes, one use).</summary>
        public void RequestCode(Action<string> done) => StartCoroutine(AskCode(done));

        /// <summary>Becomes the player of <paramref name="code"/>, joining this device's progress to theirs.</summary>
        public void RedeemCode(string code, Action<bool> done) => StartCoroutine(UseCode(code, done));

        private IEnumerator Sync()
        {
            using (UnityWebRequest request = UnityWebRequest.Get(Server + "/api/save/" + PlayerId))
            {
                request.timeout = 90;   // a free server may be waking up
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    TakeRemote(request.downloadHandler.text);
                }
            }
            Push();
        }

        private void TakeRemote(string json)
        {
            SaveData remote = null;
            try
            {
                remote = JsonUtility.FromJson<SaveData>(json);
            }
            catch (ArgumentException)
            {
                Debug.LogWarning("The server's save could not be read.");
            }
            if (remote != null && remote.updatedAt > 0)
            {
                SaveGame.Merge(remote);
                Pulled?.Invoke();
            }
        }

        private IEnumerator Upload()
        {
            pushing = true;
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(SaveGame.Data));
            using (var request = new UnityWebRequest(Server + "/api/save/" + PlayerId, "PUT"))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 90;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log("Save not uploaded now, the next night will try again: " + request.error);
                }
            }
            pushing = false;
            if (pushAgain)
            {
                pushAgain = false;
                Push();
            }
        }

        [Serializable]
        private class LinkAnswer
        {
            public string code;
            public string player;
        }

        private IEnumerator AskCode(Action<string> done)
        {
            yield return Upload();
            using (UnityWebRequest request = UnityWebRequest.PostWwwForm(Server + "/api/save/" + PlayerId + "/link", ""))
            {
                request.timeout = 90;
                yield return request.SendWebRequest();
                done(request.result == UnityWebRequest.Result.Success
                    ? JsonUtility.FromJson<LinkAnswer>(request.downloadHandler.text).code
                    : null);
            }
        }

        private IEnumerator UseCode(string code, Action<bool> done)
        {
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new LinkAnswer { code = code.Trim().ToUpperInvariant() }));
            using (var request = new UnityWebRequest(Server + "/api/save/redeem", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 90;
                yield return request.SendWebRequest();
                string player = request.result == UnityWebRequest.Result.Success
                    ? JsonUtility.FromJson<LinkAnswer>(request.downloadHandler.text).player
                    : null;
                if (player == null || !IsId(player))
                {
                    done(false);
                    yield break;
                }
                PlayerId = player;
                yield return Sync();
                done(true);
            }
        }
    }
}
