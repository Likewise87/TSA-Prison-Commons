using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PrisonCommons;

/// <summary>
/// Vanilla chew-spot search is centered on the pawn and rejects HostFaction pawns
/// standing in another room. Hungry prisoners in a private cell therefore find commons
/// food (social + reach OK) but then fail TryFindChairOrSpot and never Ingest.
/// Search chairs / stand spots from the food when it sits in linger commons.
/// </summary>
[HarmonyPatch(typeof(Toils_Ingest), nameof(Toils_Ingest.TryFindChairOrSpot))]
internal static class Toils_Ingest_TryFindChairOrSpot
{
	public static bool Prefix(Pawn pawn, Thing ingestible, ref IntVec3 cell, ref bool __result)
	{
		if (!PrisonCommons.ShouldUseCommonsChewSpot(pawn, ingestible))
			return true;

		if (!PrisonCommons.TryFindChewSpotNearFood(pawn, ingestible, out cell))
			return true;

		__result = true;
		PrisonCommonsLog.Decision(pawn, "CHEW_SPOT",
			$"commons food={ingestible.LabelShort} cell={cell} | {PrisonCommonsLog.HangoutSnapshot(pawn)}");
		return false;
	}
}
