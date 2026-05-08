using MelonLoader;
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