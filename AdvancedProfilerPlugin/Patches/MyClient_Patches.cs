using System;
using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyClient_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var myClientType = Type.GetType("VRage.Network.MyClient, VRage")!;
        var source = myClientType.GetPublicInstanceMethod("OnClientUpdate");
        var prefix = typeof(MyClient_Patches).GetNonPublicStaticMethod(nameof(Prefix_OnClientUpdate));
        var suffix = typeof(MyClient_Patches).GetNonPublicStaticMethod(nameof(Suffix_OnClientUpdate));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = myClientType.GetPublicInstanceMethod("Update");
        prefix = typeof(MyClient_Patches).GetNonPublicStaticMethod(nameof(Prefix_Update));
        suffix = typeof(MyClient_Patches).GetNonPublicStaticMethod(nameof(Suffix_Update));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = myClientType.GetPublicInstanceMethod("Serialize");
        prefix = typeof(MyClient_Patches).GetNonPublicStaticMethod(nameof(Prefix_Serialize));
        suffix = typeof(MyClient_Patches).GetNonPublicStaticMethod(nameof(Suffix_Serialize));

        pattern = ctx.GetPattern(source);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Update(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyClient.Update");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_Update(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Serialize(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyClient.Serialize");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_Serialize(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
