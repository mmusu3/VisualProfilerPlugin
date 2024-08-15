using System.Runtime.CompilerServices;
using Sandbox.Engine;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyGeneralStats_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var source = typeof(MyGeneralStats).GetPublicInstanceMethod("Update");
        var prefix = typeof(MyGeneralStats_Patches).GetNonPublicStaticMethod(nameof(Prefix_Update));
        var suffix = typeof(MyGeneralStats_Patches).GetNonPublicStaticMethod(nameof(Suffix_Update));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey Update;

        internal static void Init()
        {
            Update = ProfilerKeyCache.GetOrAdd("MyGeneralStats.Update");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Update(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.Update); return true; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_Update(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }
}
