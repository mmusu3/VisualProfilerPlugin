using System;
using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyTransportLayer_Tick_Patch
{
    public static void Patch(PatchContext ctx)
    {
        var source = Type.GetType("Sandbox.Engine.Multiplayer.MyTransportLayer, Sandbox.Game")!.GetPublicInstanceMethod("Tick");
        var prefix = typeof(MyTransportLayer_Tick_Patch).GetNonPublicStaticMethod(nameof(Prefix));
        var suffix = typeof(MyTransportLayer_Tick_Patch).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyTransportLayer.Tick");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
