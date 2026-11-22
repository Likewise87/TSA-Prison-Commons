using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PrisonCommons;

[HarmonyPatch(typeof(WorkGiver_Warden_TakeToBed), nameof(WorkGiver_Warden_TakeToBed.TryMakeJob))]
internal static class WorkGiver_Warden_TakeToBed_TryMakeJob
{
	public static void Postfix(ref Job __result, bool forced)
	{
		if (forced || __result == null || __result.def != JobDefOf.EscortPrisonerToBed)
			return;

		var prisoner = __result.targetA.Pawn;
		var currentRoom = prisoner.GetRoom();
		if (currentRoom == null)
			return;

		// Free movement across the whole access complex — no auto-escort.
		// Downed uses TakeWoundedPrisonerToBed (different def). Player forced still works.
		if (PrisonCommons.IsAccessCommons(currentRoom) || PrisonCommons.IsAllowedDoorway(prisoner, currentRoom))
			__result = null!;
	}
}
