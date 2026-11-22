using HarmonyLib;
using RimWorld;
using Verse;

namespace PrisonCommons;

/// <summary>
/// Hard gate: colonists must not start joy on For-prisoners buildings / linger-commons joy.
/// SocialProperness alone was not enough in practice (timing / comps registration).
/// </summary>
[HarmonyPatch(typeof(JoyGiver_InteractBuilding), "CanInteractWith")]
internal static class JoyGiver_InteractBuilding_CanInteractWith
{
	public static void Postfix(Pawn pawn, Thing t, bool inBed, ref bool __result)
	{
		if (!__result || pawn == null || t == null)
			return;
		if (pawn.IsPrisoner)
			return;
		if (PrisonCommons.ColonistBlockedFromJoyOn(t))
			__result = false;
	}
}
