using MCPForUnity.Editor.Services;
using UnityEditor;

/// <summary>Editor-only: connects the MCP for Unity bridge once per editor session.</summary>
[InitializeOnLoad]
internal static class McpBridgeAutoConnect
{
    private const string Key = "WeeWoo.McpBridgeAutoConnect";

    static McpBridgeAutoConnect()
    {
        if (UnityEngine.Application.isBatchMode) return;
        EditorApplication.delayCall += Connect;
    }

    private static async void Connect()
    {
        IBridgeControlService bridge = MCPServiceLocator.Bridge;
        if (bridge.IsRunning || SessionState.GetBool(Key, false)) return;
        SessionState.SetBool(Key, true);
        bool started = await bridge.StartAsync();
        UnityEngine.Debug.Log("[MCP] Bridge auto-connect " + (started ? "succeeded" : "failed"));
    }
}
