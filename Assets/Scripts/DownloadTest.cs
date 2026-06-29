using System.IO;
using UnityEngine;
using PastPort.Unity;

public class DownloadTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("=== STEP 0: Save path: " + Application.persistentDataPath);

        PastPortAuth.Instance.Login(
            email:    "me1234@gmail.com",
            password: "UY7Y2D@P6Z1XvDOKReSpzCdC$yf9XFCL!MjkmEngXGCp@cl$SO?6qjZxbiGfe3eSI%IpZnk!jG$l@?Mp9eUU9G664P74KFrG%urLG",

            onSuccess: userName =>
            {
                Debug.Log("=== STEP 1 PASSED: Logged in as " + userName);

                // ✅ SearchAndDownload is INSIDE onSuccess — login is done, token exists
                PastPortAuth.Instance.SearchAndDownload(
                    assetName: "Bamboo",
                    savePath:  Path.Combine(Application.persistentDataPath, "Bamboo_73c8b2b3.glb"),
                    onProgress: p    => Debug.Log("Progress: " + (p * 100f).ToString("F0") + "%"),
                    onSuccess:  path => Debug.Log("=== STEP 2 PASSED: Saved to " + path),
                    onBytesSuccess: bytes => Debug.Log("=== Bytes received: " + bytes.Length),
                    onError:    err  => Debug.LogError("=== STEP 2 FAILED: " + err)
                );
            },

            onError: err => Debug.LogError("=== STEP 1 FAILED: " + err)
        );
    }
}