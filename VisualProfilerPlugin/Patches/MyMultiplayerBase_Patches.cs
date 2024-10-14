using System.Runtime.CompilerServices;
using Sandbox.Engine.Multiplayer;
using Torch.Managers.PatchManager;
using VRage;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyMultiplayerBase_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var source = typeof(MyMultiplayerBase).GetNonPublicInstanceMethod("ControlMessageReceived");
        var prefix = typeof(MyMultiplayerBase_Patches).GetNonPublicStaticMethod(nameof(Prefix_ControlMessageReceived));
        var suffix = typeof(MyMultiplayerBase_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = typeof(MyMultiplayerBase).GetPublicInstanceMethod(nameof(MyMultiplayerBase.Tick));
        prefix = typeof(MyMultiplayerBase_Patches).GetNonPublicStaticMethod(nameof(Prefix_Tick));
        suffix = typeof(MyMultiplayerBase_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey ControlMessageReceived;
        internal static ProfilerKey Tick;

        internal static void Init()
        {
            ControlMessageReceived = ProfilerKeyCache.GetOrAdd("MyMultiplayerBase.ControlMessageReceived");
            Tick = ProfilerKeyCache.GetOrAdd("MyMultiplayerBase.Tick");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_ControlMessageReceived(ref ProfilerTimer __local_timer, MyPacket p)
    {
        __local_timer = Profiler.Start(Keys.ControlMessageReceived, profileMemory: true, new((long)p.Sender.Id.Value, "SenderId: {0}"));
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Tick(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start(Keys.Tick, profileMemory: true, new(ProfilerEvent.EventCategory.Network));
        return true;
    }
}
