using System.Reflection;
using System.Runtime.CompilerServices;
using Sandbox.Game.Entities.Cube;
using Torch.Managers.PatchManager;
using VRage.Utils;
using VRageMath;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyGridPhysics_Patches
{
    public static void Patch(PatchContext ctx)
    {
        PatchPrefixSuffixPair(ctx, "RigidBody_ContactPointCallback", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "PerformDeformation", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "DeformBones", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyGridPhysics.UpdateShape), _public: true, _static: false);

        {
            var source = typeof(MyGridPhysics).GetMethod(nameof(MyGridPhysics.ApplyDeformation),
                BindingFlags.Public | BindingFlags.Instance, null,
                [typeof(float), typeof(float), typeof(float), typeof(Vector3), typeof(Vector3),
                    typeof(MyStringHash), typeof(int).MakeByRefType(), typeof(float), typeof(float), typeof(long)], null);

            var prefix = typeof(MyGridPhysics_Patches).GetNonPublicStaticMethod("Prefix_" + nameof(MyGridPhysics.ApplyDeformation));
            var suffix = typeof(MyGridPhysics_Patches).GetNonPublicStaticMethod(nameof(Suffix));

            var pattern = ctx.GetPattern(source);
            pattern.Prefixes.Add(prefix);
            pattern.Suffixes.Add(suffix);
        }
    }

    static void PatchPrefixSuffixPair(PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        var source = typeof(MyGridPhysics).GetMethod(methodName, _public, _static);
        var prefix = typeof(MyGridPhysics_Patches).GetNonPublicStaticMethod("Prefix_" + methodName);
        var suffix = typeof(MyGridPhysics_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;

    [MethodImpl(Inline)] static void Suffix(ref ProfilerTimer __local_timer) => __local_timer.Stop();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_RigidBody_ContactPointCallback(ref ProfilerTimer __local_timer, MyGridPhysics __instance)
    {
        __local_timer = Profiler.Start("MyGridPhysics.RigidBody_ContactPointCallback", profileMemory: true,
            new(__instance.Entity, "Grid entity: {0}"));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_PerformDeformation(ref ProfilerTimer __local_timer, MyGridPhysics __instance)
    {
        __local_timer = Profiler.Start("MyGridPhysics.PerformDeformation", profileMemory: true,
            new(__instance.Entity, "Grid entity: {0}"));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_ApplyDeformation(ref ProfilerTimer __local_timer, MyGridPhysics __instance)
    {
        __local_timer = Profiler.Start("MyGridPhysics.ApplyDeformation", profileMemory: true,
            new(__instance.Entity, "Grid entity: {0}"));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_DeformBones(ref ProfilerTimer __local_timer, MyGridPhysics __instance)
    {
        __local_timer = Profiler.Start("MyGridPhysics.DeformBones", profileMemory: true,
            new(__instance.Entity, "Grid entity: {0}"));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateShape(ref ProfilerTimer __local_timer, MyGridPhysics __instance)
    {
        __local_timer = Profiler.Start("MyGridPhysics.UpdateShape", profileMemory: true,
            new(__instance.Entity, "Grid entity: {0}"));

        return true;
    }
}
