using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using GLTFast;
using System.Threading.Tasks;

public class ModelDownloader : MonoBehaviour
{
    [Header("API Settings")]
    [SerializeField] private string apiUrl = "http://pastpost.somee.com/api/Assets/download/Bamboo_73c8b2b3.glb";
    [SerializeField] private string bearerToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjM4OTBjOThhLWVkMzktNDQyNS05ODljLTcyYzBhMTc4NzljNSIsInN1YiI6IjM4OTBjOThhLWVkMzktNDQyNS05ODljLTcyYzBhMTc4NzljNSIsImVtYWlsIjoiZXllbHV2dTIwMDBAZ21haWwuY29tIiwianRpIjoiMTljMDRiZGQtMDg2Ni00NjhiLWJiNjgtYzY0NGIyMWU1MGFjIiwiRmlyc3ROYW1lIjoiaGVsbG8iLCJMYXN0TmFtZSI6IndvcmxkIiwiaHR0cDovL3NjaGVtYXMubWljcm9zb2Z0LmNvbS93cy8yMDA4LzA2L2lkZW50aXR5L2NsYWltcy9yb2xlIjoiSW5kaXZpZHVhbCIsImV4cCI6MTc4Mjc1Nzc3MCwiaXNzIjoiUGFzdFBvcnRBUEkiLCJhdWQiOiJQYXN0UG9ydENsaWVudHMifQ.FkWV63EEywsd7IkES3BqRJXwyWomw49PoxgBf8Iy3Fg";

    void Start()
    {
        StartCoroutine(DownloadModelCoroutine());
    }

    IEnumerator DownloadModelCoroutine()
    {
        UnityWebRequest request = UnityWebRequest.Get(apiUrl);
        request.SetRequestHeader("Authorization", "Bearer " + bearerToken);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Download failed: " + request.error);
            yield break;
        }

        byte[] glbData = request.downloadHandler.data;
        Debug.Log("Downloaded file size: " + glbData.Length + " bytes");

        _ = LoadModelIntoScene(glbData);
    }

    private async Task LoadModelIntoScene(byte[] glbData)
    {
        var gltf = new GltfImport();

        bool success = await gltf.LoadGltfBinary(glbData);

        if (success)
        {
            GameObject modelRoot = new GameObject("DownloadedStatue");
            await gltf.InstantiateMainSceneAsync(modelRoot.transform);
            Debug.Log("Model loaded successfully");
        }
        else
        {
            Debug.LogError("Failed to load model from data");
        }
    }
}