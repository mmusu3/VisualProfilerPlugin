using System.Runtime.CompilerServices;
using Sandbox.Engine.Networking;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyGameService_Update_Patch
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyGameService).GetPublicStaticMethod("Update");
        var prefix = typeof(MyGameService_Update_Patch).GetNonPublicStaticMethod(nameof(Prefix));
        var suffix = typeof(MyGameService_Update_Patch).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyGameService.Update");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
