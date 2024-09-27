using System;
using System.Runtime.CompilerServices;
using Sandbox;
using Sandbox.Game.Entities.Cube;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyTerminalBlock_Patches
{
    static Action<object> OnUnsafeSettingsChangedInternal = null!;

    public static void Patch(PatchContext ctx)
    {
        OnUnsafeSettingsChangedInternal = (Action<object>)typeof(MyTerminalBlock).GetNonPublicStaticMethod("OnUnsafeSettingsChangedInternal").CreateDelegate(typeof(Action<object>));

        var source = typeof(MyTerminalBlock).GetNonPublicInstanceMethod("OnUnsafeSettingsChanged");
        var prefix = typeof(MyTerminalBlock_Patches).GetNonPublicStaticMethod(nameof(Prefix_OnUnsafeSettingsChanged));

        ctx.GetPattern(source).Prefixes.Add(prefix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_OnUnsafeSettingsChanged(MyTerminalBlock __instance)
    {
        MySandboxGame.Static.Invoke("MyTerminalBlock.OnUnsafeSettingsChanged", __instance, OnUnsafeSettingsChangedInternal);
        return false;
    }
}
