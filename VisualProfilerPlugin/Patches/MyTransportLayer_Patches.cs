using System;
using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyTransportLayer_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var myTransportLayer = Type.GetType("Sandbox.Engine.Multiplayer.MyTransportLayer, Sandbox.Game")!;
        var source = myTransportLayer.GetPublicInstanceMethod("Tick");
        var prefix = typeof(MyTransportLayer_Patches).GetNonPublicStaticMethod(nameof(Prefix_Tick));
        var suffix = typeof(MyTransportLayer_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = myTransportLayer.GetNonPublicInstanceMethod("HandleMessage");
        prefix = typeof(MyTransportLayer_Patches).GetNonPublicStaticMethod(nameof(Prefix_HandleMessage));
        suffix = typeof(MyTransportLayer_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey Tick;
        internal static ProfilerKey HandleMessage;

        internal static void Init()
        {
            Tick = ProfilerKeyCache.GetOrAdd("MyTransportLayer.Tick");
            HandleMessage = ProfilerKeyCache.GetOrAdd("MyTransportLayer.HandleMessage");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Tick(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.Tick); return true; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_HandleMessage(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.HandleMessage); return true; }
}
