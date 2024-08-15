using System;
using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;
using VRage.Network;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyClient_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var myClientType = Type.GetType("VRage.Network.MyClient, VRage")!;
        var source = myClientType.GetPublicInstanceMethod("OnClientUpdate");
        var prefix = typeof(MyClient_Patches).GetNonPublicStaticMethod(nameof(Prefix_OnClientUpdate));
        var suffix = typeof(MyClient_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = myClientType.GetPublicInstanceMethod("Update");
        prefix = typeof(MyClient_Patches).GetNonPublicStaticMethod(nameof(Prefix_Update));
        suffix = typeof(MyClient_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = myClientType.GetPublicInstanceMethod("Serialize");
        prefix = typeof(MyClient_Patches).GetNonPublicStaticMethod(nameof(Prefix_Serialize));
        suffix = typeof(MyClient_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey OnClientUpdate;
        internal static ProfilerKey Update;
        internal static ProfilerKey Serialize;

        internal static void Init()
        {
            OnClientUpdate = ProfilerKeyCache.GetOrAdd("MyClient.OnClientUpdate");
            Update = ProfilerKeyCache.GetOrAdd("MyClient.Update");
            Serialize = ProfilerKeyCache.GetOrAdd("MyClient.Serialize");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_OnClientUpdate(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.OnClientUpdate); return true; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Update(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.OnClientUpdate); return true; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Serialize(ref ProfilerTimer __local_timer, IMyStateGroup group)
    { __local_timer = Profiler.Start(Keys.Serialize, profileMemory: true, new(group.Owner)); return true; }
}
