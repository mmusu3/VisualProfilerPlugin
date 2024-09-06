using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sandbox.Engine.Networking;
using Torch.Managers.PatchManager;
using VRage.Game;
using VRage.GameServices;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyLocalCache_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var source = typeof(MyLocalCache).GetPublicStaticMethod(nameof(MyLocalCache.SaveSector));
        var prefix = typeof(MyLocalCache_Patches).GetNonPublicStaticMethod(nameof(Prefix_SaveSector));
        var suffix = typeof(MyLocalCache_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = typeof(MyLocalCache).GetPublicStaticMethod(nameof(MyLocalCache.SaveCheckpoint), [typeof(MyObjectBuilder_Checkpoint), typeof(string), typeof(ulong).MakeByRefType(), typeof(List<MyCloudFile>)]);
        prefix = typeof(MyLocalCache_Patches).GetNonPublicStaticMethod(nameof(Prefix_SaveCheckpoint));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey SaveSector;
        internal static ProfilerKey SaveCheckpoint;

        internal static void Init()
        {
            SaveSector = ProfilerKeyCache.GetOrAdd("MyLocalCache.SaveSector");
            SaveCheckpoint = ProfilerKeyCache.GetOrAdd("MyLocalCache.SaveCheckpoint");
        }
    }

    const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;

    [MethodImpl(Inline)] static void Suffix(ref ProfilerTimer __local_timer) => __local_timer.Stop();

    [MethodImpl(Inline)] static bool Prefix_SaveSector(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.SaveSector); return true; }

    [MethodImpl(Inline)] static bool Prefix_SaveCheckpoint(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.SaveCheckpoint); return true; }
}
