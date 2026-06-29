using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace PastPort.Unity
{
    public class PastPortAuth : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────
        public static PastPortAuth Instance { get; private set; }

        [Header("API Settings")]
        [SerializeField] private string baseUrl = "http://pastpost.somee.com";

        // ── Per-user token storage (in memory) ───────────────────────
        public string AccessToken  { get; private set; }
        public string RefreshToken { get; private set; }
        public bool   IsLoggedIn   => !string.IsNullOrEmpty(AccessToken);

        // ── Lifecycle ────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Debug.Log("[PastPortAuth] Save path: " + Application.persistentDataPath);
            Debug.Log("[PastPortAuth] Base URL: " + baseUrl); // ← confirms what URL is actually stored
        }

        // ── Step 1: Login → get per-user JWT ─────────────────────────
        public void Login(string email, string password,
                          Action<string> onSuccess = null,
                          Action<string> onError   = null)
            => StartCoroutine(LoginCoroutine(email, password, onSuccess, onError));

        private IEnumerator LoginCoroutine(string email, string password,
                                           Action<string> onSuccess,
                                           Action<string> onError)
        {
            // ✅ FIX: build URL as a string FIRST, then log it — never log the request object
            string loginUrl = baseUrl + "/api/Auth/login";
            Debug.Log("=== HITTING URL: [" + loginUrl + "]");

            var payload = JsonUtility.ToJson(new LoginRequest { email = email, password = password });
            Debug.Log("=== REQUEST BODY: " + payload);

            var bodyRaw = System.Text.Encoding.UTF8.GetBytes(payload);

            using var req = new UnityWebRequest(loginUrl, "POST");
            req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            // ✅ Log the full response so we can see exactly what the server returns
            Debug.Log("=== RESPONSE CODE: " + req.responseCode);
            Debug.Log("=== RESPONSE BODY: " + req.downloadHandler.text);

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(req.error);
                yield break;
            }

            var res = JsonUtility.FromJson<LoginResponse>(req.downloadHandler.text);
            if (!res.success || string.IsNullOrEmpty(res.token))
            {
                onError?.Invoke(res.message ?? "Login failed");
                yield break;
            }

            AccessToken  = res.token;
            RefreshToken = res.refreshToken;
            Debug.Log("[PastPortAuth] Logged in as: " + email);
            onSuccess?.Invoke(res.userName ?? email);
        }

        // ── Step 2: Download with Authorization header ────────────────
        public void DownloadAsset(string fileName      = "Bamboo",
                                  string savePath              = null,
                                  Action<string> onSuccess      = null,
                                  Action<byte[]> onBytesSuccess = null,
                                  Action<string> onError        = null,
                                  Action<float>  onProgress     = null)
        {
            if (!IsLoggedIn) { onError?.Invoke("Not authenticated. Call Login() first."); return; }
            StartCoroutine(DownloadCoroutine(fileName, savePath, onSuccess, onBytesSuccess, onError, onProgress, false));
        }

        private IEnumerator DownloadCoroutine(string fileName, string savePath,
                                              Action<string> onSuccess, Action<byte[]> onBytesSuccess,
                                              Action<string> onError,   Action<float>  onProgress,
                                              bool isRetry)
        {
            string downloadUrl = baseUrl + "/api/Assets/download/" + UnityWebRequest.EscapeURL(fileName);
            Debug.Log("=== DOWNLOAD URL: [" + downloadUrl + "]");

            using var req = UnityWebRequest.Get(downloadUrl);
            req.SetRequestHeader("Authorization", "Bearer " + AccessToken);
            req.downloadHandler = new DownloadHandlerBuffer();

            var op = req.SendWebRequest();
            while (!op.isDone) { onProgress?.Invoke(req.downloadProgress); yield return null; }
            onProgress?.Invoke(1f);

            Debug.Log("=== DOWNLOAD RESPONSE CODE: " + req.responseCode);

            // Auto-refresh on 401 and retry once
            if (req.responseCode == 401 && !isRetry)
            {
                Debug.LogWarning("[PastPortAuth] 401 — attempting token refresh.");
                bool ok = false; string refreshErr = null;
                yield return StartCoroutine(RefreshCoroutine(() => ok = true, e => refreshErr = e));
                if (!ok) { onError?.Invoke("Session expired. Log in again. (" + refreshErr + ")"); yield break; }
                yield return StartCoroutine(DownloadCoroutine(fileName, savePath, onSuccess, onBytesSuccess, onError, onProgress, true));
                yield break;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[PastPortAuth] Download failed: " + req.error + " | Body: " + req.downloadHandler.text);
                onError?.Invoke(req.error);
                yield break;
            }

            var bytes = req.downloadHandler.data;
            Debug.Log("[PastPortAuth] Downloaded '" + fileName + "' — " + bytes.Length.ToString("N0") + " bytes");
            onBytesSuccess?.Invoke(bytes);

            if (!string.IsNullOrEmpty(savePath))
            {
                try
                {
                    var dir = Path.GetDirectoryName(savePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    File.WriteAllBytes(savePath, bytes);
                    Debug.Log("[PastPortAuth] Saved to: " + savePath);
                    onSuccess?.Invoke(savePath);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[PastPortAuth] Save failed: " + ex.Message);
                    onError?.Invoke("File save error: " + ex.Message);
                }
            }
        }

        // ── Token Refresh ─────────────────────────────────────────────
        private IEnumerator RefreshCoroutine(Action onSuccess, Action<string> onError)
        {
            if (string.IsNullOrEmpty(RefreshToken)) { onError?.Invoke("No refresh token"); yield break; }

            var bodyRaw = System.Text.Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(new RefreshRequest { refreshToken = RefreshToken }));

            using var req = new UnityWebRequest(baseUrl + "/api/Auth/refresh-token", "POST");
            req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                AccessToken = null; RefreshToken = null;
                onError?.Invoke(req.error);
                yield break;
            }

            var res = JsonUtility.FromJson<LoginResponse>(req.downloadHandler.text);
            AccessToken  = res.token;
            RefreshToken = res.refreshToken;
            onSuccess?.Invoke();
        }

        // ── Logout ────────────────────────────────────────────────────
        public void Logout() { AccessToken = null; RefreshToken = null; }

        // ── DTOs ──────────────────────────────────────────────────────
        [Serializable] private class LoginRequest   { public string email, password; }
        [Serializable] private class RefreshRequest { public string refreshToken; }
        [Serializable] private class LoginResponse  { public bool success; public string token, refreshToken, userName, message; }

        [Serializable] private class AssetSearchResponse { public bool success; public AssetSearchData data; }
[Serializable] private class AssetSearchData     { public string id; public string fileName; public string name; }

public void SearchAndDownload(string assetName,
                              string savePath               = null,
                              Action<string> onSuccess       = null,
                              Action<byte[]> onBytesSuccess  = null,
                              Action<string> onError         = null,
                              Action<float>  onProgress      = null)
{
    if (!IsLoggedIn) { onError?.Invoke("Not authenticated. Call Login() first."); return; }
    StartCoroutine(SearchAndDownloadCoroutine(assetName, savePath, onSuccess, onBytesSuccess, onError, onProgress));
}

private IEnumerator SearchAndDownloadCoroutine(string assetName, string savePath,
                                               Action<string> onSuccess, Action<byte[]> onBytesSuccess,
                                               Action<string> onError,   Action<float>  onProgress)
{
    // Step 1: Search by human-readable name → get asset ID + real fileName
    string searchUrl = baseUrl + "/api/UnityAssets/search?name=" + UnityWebRequest.EscapeURL(assetName);
    Debug.Log("[PastPortAuth] Searching: " + searchUrl);

    using var searchReq = UnityWebRequest.Get(searchUrl);
    // Search is AllowAnonymous but we send token anyway — no harm
    searchReq.SetRequestHeader("Authorization", "Bearer " + AccessToken);
    yield return searchReq.SendWebRequest();

    if (searchReq.result != UnityWebRequest.Result.Success)
    {
        onError?.Invoke("Search failed: " + searchReq.error); yield break;
    }

    var searchRes = JsonUtility.FromJson<AssetSearchResponse>(searchReq.downloadHandler.text);
    if (!searchRes.success || searchRes.data == null || string.IsNullOrEmpty(searchRes.data.id))
    {
        onError?.Invoke("Asset '" + assetName + "' not found."); yield break;
    }

    string assetId       = searchRes.data.id;
    string realFileName  = searchRes.data.fileName;
    Debug.Log("[PastPortAuth] Found → ID: " + assetId + " | File: " + realFileName);

    // Step 2: Download by ID using the correct Unity endpoint
    string downloadUrl = baseUrl + "/api/UnityAssets/download/" + assetId;
    Debug.Log("[PastPortAuth] Downloading: " + downloadUrl);

    using var dlReq = UnityWebRequest.Get(downloadUrl);
    dlReq.SetRequestHeader("Authorization", "Bearer " + AccessToken);
    dlReq.downloadHandler = new DownloadHandlerBuffer();

    var op = dlReq.SendWebRequest();
    while (!op.isDone) { onProgress?.Invoke(dlReq.downloadProgress); yield return null; }
    onProgress?.Invoke(1f);

    if (dlReq.result != UnityWebRequest.Result.Success)
    {
        Debug.LogError("[PastPortAuth] Download failed: " + dlReq.error + " | " + dlReq.downloadHandler.text);
        onError?.Invoke(dlReq.error); yield break;
    }

    var bytes = dlReq.downloadHandler.data;
    Debug.Log("[PastPortAuth] Downloaded '" + realFileName + "' — " + bytes.Length.ToString("N0") + " bytes");
    onBytesSuccess?.Invoke(bytes);

    if (!string.IsNullOrEmpty(savePath))
    {
        var dir = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(savePath, bytes);
        Debug.Log("[PastPortAuth] Saved to: " + savePath);
        onSuccess?.Invoke(savePath);
    }
}
    }
}