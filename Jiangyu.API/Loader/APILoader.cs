using MelonLoader;
using HarmonyLib;
using Jiangyu.API;
using Jiangyu.API.Internal;

[assembly: MelonInfo(typeof(Jiangyu.API.APILoader), "Jiangyu API", "1.0.0", "Pylkij")]
[assembly: MelonGame(null, null)]

namespace Jiangyu.API;

public class APILoader : MelonMod
{
    public override void OnInitializeMelon()
    {
        OffsetCache.Initialize();
        LoggerInstance.Msg("Jiangyu API initialised.");

        // Initialize tactical events for C# event subscriptions
        // Patches TacticalManager.InvokeOnX methods to fire API events
        TacticalEvents.Initialize(HarmonyInstance);
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        GameState.NotifySceneLoaded(sceneName);
    }

    public override void OnUpdate()
    {
        Defer.ProcessUpdate();
    }
}