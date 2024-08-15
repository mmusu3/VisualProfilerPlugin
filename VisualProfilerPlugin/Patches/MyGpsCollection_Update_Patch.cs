using System.Runtime.CompilerServices;
using Sandbox.Game.Multiplayer;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyGpsCollection_Update_Patch
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var source = typeof(MyGpsCollection).GetPublicInstanceMethod(nameof(MyGpsCollection.Update));
        var prefix = typeof(MyGpsCollection_Update_Patch).GetNonPublicStaticMethod(nameof(Prefix_Update));
        var suffix = typeof(MyGpsCollection_Update_Patch).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey Update;

        internal static void Init()
        {
            Update = ProfilerKeyCache.GetOrAdd("MyGpsCollection.Update");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Update(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.Update); return true; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }
}
