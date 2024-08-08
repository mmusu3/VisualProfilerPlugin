using System.Runtime.CompilerServices;
using Sandbox.Game.Multiplayer;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyGpsCollection_Update_Patch
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyGpsCollection).GetPublicInstanceMethod(nameof(MyGpsCollection.Update));
        var prefix = typeof(MyGpsCollection_Update_Patch).GetNonPublicStaticMethod(nameof(Prefix_Update));
        var suffix = typeof(MyGpsCollection_Update_Patch).GetNonPublicStaticMethod(nameof(Suffix_Update));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Update(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyGpsCollection.Update");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_Update(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
