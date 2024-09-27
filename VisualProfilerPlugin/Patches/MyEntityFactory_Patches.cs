using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;
using VRage.ObjectBuilders;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyEntityFactory_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        PatchPrefixSuffixPair(ctx, Type.GetType("Sandbox.Game.Entities.MyEntityFactory, Sandbox.Game")!.GetMethod("CreateEntity", [typeof(MyObjectBuilderType), typeof(string)])!);
    }

    static void PatchPrefixSuffixPair(PatchContext patchContext, MethodInfo method)
    {
        if (method == null) throw new ArgumentNullException(nameof(method));

        var source = method;
        var prefix = typeof(MyEntityFactory_Patches).GetNonPublicStaticMethod("Prefix_" + method.Name);
        var suffix = typeof(MyEntityFactory_Patches).GetNonPublicStaticMethod("Suffix");

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey CreateEntity;

        internal static void Init()
        {
            CreateEntity = ProfilerKeyCache.GetOrAdd("MyEntityFactory.CreateEntity");
        }
    }

    const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;

    [MethodImpl(Inline)] static void Suffix(ref ProfilerTimer __local_timer) => __local_timer.Stop();

    [MethodImpl(Inline)] static bool Prefix_CreateEntity(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.CreateEntity); return true; }
}
