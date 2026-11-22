using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PrisonCommons;

/// <summary>
/// Vanilla ends joy jobs immediately when needs.joy is null (prisoners).
/// Keep the session going for prisoners, then grant a mood thought and succeed.
/// Priority First so other JoyTick patches (e.g. Easy Reading) cannot restore the null-joy abort.
/// </summary>
[HarmonyPatch(typeof(JoyUtility), nameof(JoyUtility.JoyTickCheckEnd))]
[HarmonyPriority(Priority.First)]
internal static class JoyUtility_JoyTickCheckEnd_Prisoner
{
	public static bool Prefix(
		Pawn pawn,
		int delta,
		JoyTickFullJoyAction fullJoyAction,
		float extraJoyGainFactor,
		Building joySource,
		ref bool __result)
	{
		if (pawn is not { IsPrisoner: true, Spawned: true })
			return true;
		if (pawn.needs?.joy != null)
			return true; // another mod gave them joy — use vanilla

		Job? curJob = pawn.CurJob;
		if (curJob?.def?.joyKind == null)
			return true;

		if (joySource != null)
			extraJoyGainFactor *= joySource.GetStatValue(StatDefOf.JoyGainFactor);

		if (curJob.def.joySkill != null)
			pawn.skills?.GetSkill(curJob.def.joySkill)?.Learn(curJob.def.joyXpPerTick * delta);

		// SitFacingBuilding also ends on Delay(joyDuration); mirror that clock without a joy bar.
		int elapsed = Find.TickManager.TicksGame - curJob.startTick;
		int duration = curJob.def.joyDuration > 0 ? curJob.def.joyDuration : 4000;
		duration = (int)(duration * extraJoyGainFactor);
		if (duration < 1500)
			duration = 1500;

		if (elapsed >= duration)
		{
			PrisonerRecreation.NotifyPlayFinished(pawn);
			pawn.jobs?.curDriver?.EndJobWith(JobCondition.Succeeded);
			__result = true;
			return false;
		}

		__result = false;
		return false; // skip vanilla null-joy InterruptForced
	}
}

/// <summary>
/// Chess/poker SitFacingBuilding FinishAction calls this even when the toil ends on Delay.
/// Grants the prisoner mood thought (stackLimit 1; safe if JoyTick also finished the job).
/// </summary>
[HarmonyPatch(typeof(JoyUtility), nameof(JoyUtility.TryGainRecRoomThought))]
internal static class JoyUtility_TryGainRecRoomThought_Prisoner
{
	public static void Postfix(Pawn pawn)
	{
		if (pawn is not { IsPrisoner: true })
			return;
		if (pawn.CurJob?.def?.joyKind == null)
			return;
		PrisonerRecreation.NotifyPlayFinished(pawn);
	}
}
