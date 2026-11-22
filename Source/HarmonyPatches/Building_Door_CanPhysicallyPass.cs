using HarmonyLib;
using RimWorld;
using Verse;

namespace PrisonCommons;

/// <summary>
/// ReBuild blocks hostile prisoners on cell doors via CanPhysicallyPass.
/// Colony prisoners must still pass for commons / bathroom access.
/// </summary>
[HarmonyPatch(typeof(Building_Door), nameof(Building_Door.CanPhysicallyPass))]
[HarmonyPriority(Priority.Last)]
internal static class Building_Door_CanPhysicallyPass
{
	public static void Postfix(Building_Door __instance, Pawn p, ref bool __result)
	{
		if (__result || p == null)
			return;

		if (!CellDoorPass.CanColonyPrisonerPass(p, __instance))
			return;

		__result = true;
	}
}
