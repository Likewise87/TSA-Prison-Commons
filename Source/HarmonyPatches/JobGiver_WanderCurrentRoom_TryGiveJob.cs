using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PrisonCommons;

/// <summary>
/// Prisoners idle with WanderCurrentRoom:
/// 1) Vacate leave bathrooms / leave-transit doorways.
/// 2) Daytime: prefer hangout at linger commons (distinct room or near marker).
/// </summary>
[HarmonyPatch]
internal static class JobGiver_WanderCurrentRoom_TryGiveJob
{
	static MethodBase TargetMethod()
		=> AccessTools.DeclaredMethod(typeof(JobGiver_Wander), "TryGiveJob")
		   ?? throw new InvalidOperationException("JobGiver_Wander.TryGiveJob not found");

	public static bool Prefix(JobGiver_Wander __instance, Pawn pawn, ref Job __result)
	{
		if (__instance is not JobGiver_WanderCurrentRoom)
			return true;

		if (pawn is not { IsPrisoner: true, Spawned: true })
			return true;

		if (PrisonCommons.ShouldVacateLeaveArea(pawn))
		{
			var vacate = PrisonCommons.MakeVacateLeaveRoomJob(pawn);
			if (vacate != null)
			{
				__result = vacate;
				PrisonCommonsLog.Decision(pawn, "VACATE",
					$"{PrisonCommonsLog.JobLabel(vacate)} | {PrisonCommonsLog.HangoutSnapshot(pawn)}");
				return false;
			}

			PrisonCommonsLog.Decision(pawn, "VACATE_FAIL",
				$"no dest | {PrisonCommonsLog.HangoutSnapshot(pawn)}");
			return true;
		}

		if (PrisonerRecreation.ShouldTryPlay(pawn))
		{
			var play = PrisonerRecreation.TryMakePlayJob(pawn);
			if (play != null)
			{
				__result = play;
				PrisonCommonsLog.Decision(pawn, "RECREATE",
					$"{PrisonCommonsLog.JobLabel(play)} | {PrisonCommonsLog.HangoutSnapshot(pawn)}");
				return false;
			}
		}

		if (PrisonCommons.ShouldPreferLingerCommons(pawn))
		{
			var hangout = PrisonCommons.MakePreferLingerCommonsJob(pawn);
			if (hangout != null)
			{
				__result = hangout;
				PrisonCommonsLog.Decision(pawn, "HANGOUT",
					$"{PrisonCommonsLog.JobLabel(hangout)} | {PrisonCommonsLog.HangoutSnapshot(pawn)}");
				return false;
			}

			PrisonCommonsLog.Decision(pawn, "HANGOUT_FAIL",
				$"job null | {PrisonCommons.DiagnosePreferLinger(pawn)} | {PrisonCommonsLog.HangoutSnapshot(pawn)}");
			return true;
		}

		// Only log interesting idle skips (expected night/tired/near-marker stay silent).
		if (PrisonCommons.IsDaytimeForHangout(pawn) &&
		    !PrisonCommons.IsNearActiveLingerMarker(pawn) &&
		    (pawn.needs?.rest == null || pawn.needs.rest.CurLevel >= Need_Rest.ThreshTired))
		{
			var why = PrisonCommons.DiagnosePreferLinger(pawn);
			if (why.Contains("no active linger") || why.Contains("no reachable"))
			{
				PrisonCommonsLog.Decision(pawn, "HANGOUT_FAIL",
					$"{why} | {PrisonCommonsLog.HangoutSnapshot(pawn)}");
			}
		}

		return true;
	}
}
