using System.Collections.Generic;
using System.Reflection;
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

        PatchPrefixSuffixPair(ctx, nameof(MyLocalCache.LoadSector), _public: false, _static: true);
        PatchPrefixSuffixPair(ctx, nameof(MyLocalCache.SaveSector), _public: true, _static: true);

        var source = typeof(MyLocalCache).GetPublicStaticMethod(nameof(MyLocalCache.SaveCheckpoint),
            [typeof(MyObjectBuilder_Checkpoint), typeof(string), typeof(ulong).MakeByRefType(), typeof(List<MyCloudFile>)]);

        PatchPrefixSuffixPair(ctx, source);
    }

    static void PatchPrefixSuffixPair(PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        var source = typeof(MyLocalCache).GetMethod(methodName, _public, _static);

        PatchPrefixSuffixPair(patchContext, source);
    }

    static void PatchPrefixSuffixPair(PatchContext patchContext, MethodInfo source)
    {
        var prefix = typeof(MyLocalCache_Patches).GetNonPublicStaticMethod("Prefix_" + source.Name);
        var suffix = typeof(MyLocalCache_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey LoadSector;
        internal static ProfilerKey SaveSector;
        internal static ProfilerKey SaveCheckpoint;

        internal static void Init()
        {
            LoadSector = ProfilerKeyCache.GetOrAdd("MyLocalCache.LoadSector");
            SaveSector = ProfilerKeyCache.GetOrAdd("MyLocalCache.SaveSector");
            SaveCheckpoint = ProfilerKeyCache.GetOrAdd("MyLocalCache.SaveCheckpoint");
        }
    }

    const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;

    [MethodImpl(Inline)] static void Suffix(ref ProfilerTimer __local_timer) => __local_timer.Stop();

    [MethodImpl(Inline)] static bool Prefix_LoadSector(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.LoadSector); return true; }

    [MethodImpl(Inline)] static bool Prefix_SaveSector(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.SaveSector); return true; }

    [MethodImpl(Inline)]
    static bool Prefix_SaveCheckpoint(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start(Keys.SaveCheckpoint, ProfilerTimerOptions.ProfileMemory, new(ProfilerEvent.EventCategory.Save));
        return true;
    }
}
