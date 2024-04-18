using System;
using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyClient_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var source = Type.GetType("VRage.Network.MyClient, VRage")!.GetPublicInstanceMethod("OnClientUpdate");
        var prefix = typeof(MyClient_Patches).GetNonPublicStaticMethod(nameof(Prefix_OnClientUpdate));
        var suffix = typeof(MyClient_Patches).GetNonPublicStaticMethod(nameof(Suffix_OnClientUpdate));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_OnClientUpdate(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyClient.OnClientUpdate");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_OnClientUpdate(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
