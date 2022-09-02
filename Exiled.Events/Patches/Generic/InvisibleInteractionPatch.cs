// -----------------------------------------------------------------------
// <copyright file="InvisibleInteractionPatch.cs" company="Exiled Team">
// Copyright (c) Exiled Team. All rights reserved.
// Licensed under the CC BY-SA 3.0 license.
// </copyright>
// -----------------------------------------------------------------------

namespace Exiled.Events.Patches.Generic
{
#pragma warning disable SA1313
#pragma warning disable SA1402
    using System.Collections.Generic;
    using System.Reflection.Emit;

    using Exiled.API.Enums;
    using Exiled.API.Features;

    using HarmonyLib;

    using Interactables;
    using Interactables.Verification;

    using UnityEngine;

    using NorthwoodLib.Pools;

    using static HarmonyLib.AccessTools;
    using CustomPlayerEffects;

    /// <summary>
    /// Patches <see cref="StandardDistanceVerification.ServerCanInteract(ReferenceHub, InteractableCollider)"/>.
    /// Implements <see cref="Player.InteractionInvisibilityRemove"/> property logic.
    /// </summary>
    [HarmonyPatch(typeof(StandardDistanceVerification), nameof(StandardDistanceVerification.ServerCanInteract))]
    internal static class InvisibleInteractionPatch
    {
        private static bool Prefix(StandardDistanceVerification __instance, ReferenceHub hub, InteractableCollider collider, ref bool __result)
        {
            if (!__instance._allowHandcuffed && !PlayerInteract.CanDisarmedInteract && hub.interCoordinator.Handcuffed)
            {
                __result = false;

                return false;
            }

            if (Vector3.Distance(hub.playerMovementSync.RealModelPosition, collider.transform.position + collider.transform.TransformDirection(collider.VerificationOffset)) < __instance._maxDistance * 1.4f)
            {
                __result = true;

                if (!Player.TryGet(hub, out Player player) || player.InteractionInvisibilityRemove)
                    return false;

                player.DisableEffect(EffectType.Invisible);
            }

            return false;
        }
    }

    /// <summary>
    /// Patches <see cref="PlayerInteract.OnInteract"/> method
    /// </summary>
    [HarmonyPatch(typeof(PlayerInteract), nameof(PlayerInteract.OnInteract))]
    internal static class InvisibleInteractionPatch2
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> newInstructions = ListPool<CodeInstruction>.Shared.Rent(instructions);

            LocalBuilder player = generator.DeclareLocal(typeof(Player));

            Label ret = generator.DefineLabel();

            int offset = -3;
            int index = newInstructions.FindIndex(i => i.Calls(PropertySetter(typeof(PlayerEffect), nameof(PlayerEffect.Intensity)))) + offset;

            newInstructions.InsertRange(index, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, Field(typeof(PlayerInteract), nameof(PlayerInteract._hub))),
                new(OpCodes.Call, Method(typeof(Player), nameof(Player.Get), new[] { typeof(ReferenceHub) })),
                new(OpCodes.Dup),
                new(OpCodes.Stloc_S, player.LocalIndex),
                new(OpCodes.Brfalse_S, ret),
                new(OpCodes.Ldloc_S, player.LocalIndex),
                new(OpCodes.Callvirt, PropertyGetter(typeof(Player), nameof(Player.InteractionInvisibilityRemove))),
                new(OpCodes.Brtrue_S, ret),
            });

            newInstructions[newInstructions.Count - 1].labels.Add(ret);

            for (int z = 0; z < newInstructions.Count; z++)
                yield return newInstructions[z];

            ListPool<CodeInstruction>.Shared.Return(newInstructions);
        }
    }
}