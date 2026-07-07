#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps MCP for Unity reachable for Codex by starting the local HTTP server in the editor.
/// </summary>
[InitializeOnLoad]
public static class CodexUnityMcpBootstrap
{
    private const string UvxPath = @"C:\Users\HOME\AppData\Roaming\Python\Python310\Scripts\uvx.exe";
    private const string BaseUrl = "http://127.0.0.1:8080";
    private const string AutoStartKey = "MCPForUnity.AutoStartOnLoad";
    private const string UvxPathKey = "MCPForUnity.UvxPath";
    private const string SetupCompletedKey = "MCPForUnity.SetupCompleted";
    private const string SetupDismissedKey = "MCPForUnity.SetupDismissed";

    static CodexUnityMcpBootstrap()
    {
        EditorApplication.delayCall += () => _ = StartAsync();
    }

    private static async Task StartAsync()
    {
        try
        {
            EditorPrefs.SetString(UvxPathKey, UvxPath);
            EditorPrefs.SetBool(AutoStartKey, true);
            EditorPrefs.SetBool(SetupCompletedKey, true);
            EditorPrefs.SetBool(SetupDismissedKey, true);

            EditorConfigurationCache.Instance.SetUseHttpTransport(true);
            EditorConfigurationCache.Instance.SetHttpTransportScope("local");
            HttpEndpointUtility.SaveLocalBaseUrl(BaseUrl);

            var server = MCPServiceLocator.Server;
            if (!server.IsLocalHttpServerReachable())
            {
                server.StartLocalHttpServer(quiet: true);
            }

            for (var attempt = 0; attempt < 60; attempt++)
            {
                if (server.IsLocalHttpServerReachable())
                {
                    await MCPServiceLocator.Bridge.StartAsync();
                    Debug.Log($"[CodexUnityMcpBootstrap] MCP for Unity is ready at {BaseUrl}/mcp");
                    return;
                }

                await Task.Delay(500);
            }

            Debug.LogWarning("[CodexUnityMcpBootstrap] MCP for Unity server did not become reachable in time.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CodexUnityMcpBootstrap] Failed to start MCP for Unity: {ex.Message}");
        }
    }
}
#endif
