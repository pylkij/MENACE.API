using System;
using System.Linq;
using System.Reflection;

namespace Jiangyu.API;

/// <summary>
/// Scene awareness, game assembly state.
/// </summary>
public static class GameState
{
    public static string CurrentScene { get; private set; } = "";

    public static event Action<string> SceneLoaded;

    /// <summary>
    /// Check if the current scene matches the given name (case-insensitive).
    /// </summary>
    public static bool IsScene(string sceneName)
    {
        return string.Equals(CurrentScene, sceneName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True if currently in the Tactical (combat) scene.
    /// </summary>
    public static bool IsTactical => IsScene("Tactical");

    /// <summary>
    /// True if currently in a strategy/campaign scene (not tactical, not menu).
    /// </summary>
    public static bool IsStrategy =>
        !string.IsNullOrEmpty(CurrentScene) &&
        !IsTactical &&
        !IsScene("Title");

    /// <summary>
    /// The Assembly-CSharp assembly, or null if not yet loaded.
    /// </summary>
    public static Assembly GameAssembly
    {
        get
        {
            try
            {
                return AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            }
            catch (Exception ex)
            {
                APILogger.ReportInternal("GameState.GameAssembly", "Failed to enumerate assemblies", ex);
                return null;
            }
        }
    }

    public static bool IsGameAssemblyLoaded => GameAssembly != null;

    /// <summary>
    /// Find a managed type by full name in Assembly-CSharp.
    /// </summary>
    public static Type FindManagedType(string fullName)
    {
        if (string.IsNullOrEmpty(fullName))
            return null;

        try
        {
            var asm = GameAssembly;
            if (asm == null) return null;

            return asm.GetTypes().FirstOrDefault(t =>
                t.FullName == fullName || t.Name == fullName);
        }
        catch (Exception ex)
        {
            APILogger.ReportInternal("GameState.FindManagedType", $"Failed for '{fullName}'", ex);
            return null;
        }
    }

    public static void NotifySceneLoaded(string sceneName)
    {
        CurrentScene = sceneName ?? "";

        try { SceneLoaded?.Invoke(sceneName); }
        catch (Exception ex)
        {
            APILogger.ReportInternal("GameState.SceneLoaded", "Event handler failed", ex);
        }
    }
}