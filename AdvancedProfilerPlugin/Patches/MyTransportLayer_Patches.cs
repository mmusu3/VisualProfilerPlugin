using System;
using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyTransportLayer_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var myTransportLayer = Type.GetType("Sandbox.Engine.Multiplayer.MyTransportLayer, Sandbox.Game")!;
        var source = myTransportLayer.GetPublicInstanceMethod("Tick");
        var prefix = typeof(MyTransportLayer_Patches).GetNonPublicStaticMethod(nameof(Prefix_Tick));
        var suffix = typeof(MyTransportLayer_Patches).GetNonPublicStaticMethod(nameof(Suffix_Tick));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = myTransportLayer.GetNonPublicInstanceMethod("HandleMessage");
        prefix = typeof(MyTransportLayer_Patches).GetNonPublicStaticMethod(nameof(Prefix_HandleMessage));
        suffix = typeof(MyTransportLayer_Patches).GetNonPublicStaticMethod(nameof(Suffix_HandleMessage));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Tick(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyTransportLayer.Tick");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_Tick(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_HandleMessage(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyTransportLayer.HandleMessage");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_HandleMessage(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
