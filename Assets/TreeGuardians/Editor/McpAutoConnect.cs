using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

namespace TreeGuardians.Editor
{
    /// Connects the MCP for Unity bridge to the local HTTP server after every domain reload, without opening the MCP window.
    [InitializeOnLoad]
    public static class McpAutoConnect
    {
        const string AutoStartPref = "MCPForUnity.AutoStartOnLoad";
        static double nextAttempt;
        static int attempts;

        static McpAutoConnect()
        {
            if (Application.isBatchMode) return;
            if (!EditorPrefs.GetBool(AutoStartPref, false)) EditorPrefs.SetBool(AutoStartPref, true);
            nextAttempt = EditorApplication.timeSinceStartup + 2.0;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextAttempt) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { nextAttempt = EditorApplication.timeSinceStartup + 1.0; return; }
            var bridge = MCPServiceLocator.Bridge;
            if (bridge.IsRunning) { EditorApplication.update -= Tick; return; }
            if (++attempts > 20) { EditorApplication.update -= Tick; Debug.LogWarning("[TG] MCP bridge could not be started automatically; open Window > MCP For Unity and press Start Session."); return; }
            nextAttempt = EditorApplication.timeSinceStartup + 5.0;
            if (!MCPServiceLocator.Server.IsLocalHttpServerReachable())
                MCPServiceLocator.Server.StartLocalHttpServer(true);
            _ = StartAsync(bridge);
        }

        static async System.Threading.Tasks.Task StartAsync(IBridgeControlService bridge)
        {
            try
            {
                bool ok = await bridge.StartAsync();
                if (ok) Debug.Log("[TG] MCP bridge session connected.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[TG] MCP bridge start failed: " + e.Message);
            }
        }
    }
}
