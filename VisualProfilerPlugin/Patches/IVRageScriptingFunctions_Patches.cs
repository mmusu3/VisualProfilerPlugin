using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;
using VRage.Scripting;

namespace VisualProfiler.Patches;

[PatchShim]
static class IVRageScriptingFunctions_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var source = typeof(IVRageScriptingFunctions).GetPublicStaticMethod(nameof(IVRageScriptingFunctions.CompileIngameScriptAsync));
        var prefix = typeof(IVRageScriptingFunctions_Patches).GetNonPublicStaticMethod(nameof(Prefix_CompileIngameScriptAsync));
        var suffix = typeof(IVRageScriptingFunctions_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey CompileIngameScriptAsync;

        internal static void Init()
        {
            CompileIngameScriptAsync = ProfilerKeyCache.GetOrAdd("CompileIngameScriptAsync");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_CompileIngameScriptAsync(ref ProfilerTimer __local_timer, string program)
    {
        __local_timer = Profiler.Start(Keys.CompileIngameScriptAsync, ProfilerTimerOptions.ProfileMemory,
            new(program.Length, "Program Length: {0:N0}"));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer) => __local_timer.Stop();
}
