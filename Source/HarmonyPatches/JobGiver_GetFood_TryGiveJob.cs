using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PrisonCommons;

/// <summary>
/// If a colony prisoner is hungry and vanilla GetFood found nothing (or aborted after
/// chew-spot failure), try food that is already in a reachable linger commons.
/// </summary>
[HarmonyPatch]
internal static class JobGiver_GetFood_TryGiveJob
{
	static MethodBase TargetMethod()
		=> AccessTools.DeclaredMethod(typeof(JobGiver_GetFood), "TryGiveJob")
		   ?? throw new System.InvalidOperationException("JobGiver_GetFood.TryGiveJob not found");

	public static void Postfix(Pawn pawn, ref Job __result)
	{
		if (__result != null)
			return;
		if (pawn is not { IsPrisonerOfColony: true, Spawned: true })
			return;
		if (FoodUtility.ShouldBeFedBySomeone(pawn))
			return;

		Need_Food? foodNeed = pawn.needs?.food;
		if (foodNeed == null)
			return;
		if (foodNeed.CurLevelPercentage >= pawn.RaceProps.FoodLevelPercentageWantEat)
			return;

		if (!PrisonCommons.TryFindCommonsFoodFor(pawn, out Thing food, out ThingDef foodDef))
			return;

		if (!Toils_Ingest.TryFindChairOrSpot(pawn, food, out _))
		{
			PrisonCommonsLog.Decision(pawn, "GETFOOD_FAIL",
				$"commons food={food.LabelShort} but no chew spot | {PrisonCommonsLog.HangoutSnapshot(pawn)}");
			return;
		}

		float nutrition = FoodUtility.GetNutrition(pawn, food, foodDef);
		Job job = JobMaker.MakeJob(JobDefOf.Ingest, food);
		job.count = FoodUtility.WillIngestStackCountOf(pawn, foodDef, nutrition);
		__result = job;
		PrisonCommonsLog.Decision(pawn, "GETFOOD",
			$"commons {PrisonCommonsLog.JobLabel(job)} food={food.LabelShort} | {PrisonCommonsLog.HangoutSnapshot(pawn)}");
	}
}
