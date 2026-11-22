using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace PrisonCommons;

/// <summary>
/// Prisoners have no Joy need; we still let them take building joy jobs in linger commons
/// and grant a mood thought when a play session finishes (no recreation bar).
/// </summary>
internal static class PrisonerRecreation
{
	/// <summary>While already in linger commons — prefer play over endless wander.</summary>
	private const float TryPlayChanceInCommons = 0.85f;
	/// <summary>While leaving cell toward hangout — can path straight to a game.</summary>
	private const float TryPlayChanceEnRoute = 0.55f;

	private static readonly List<JoyGiverDef> giverScratch = new();

	public static Job? TryMakePlayJob(Pawn pawn)
	{
		if (pawn is not { IsPrisoner: true, Spawned: true, Map: not null })
			return null;
		if (!PrisonCommons.IsDaytimeForHangout(pawn))
			return null;
		if (pawn.needs?.rest != null && pawn.needs.rest.CurLevel < Need_Rest.ThreshTired)
			return null;
		if (pawn.Downed || pawn.InMentalState)
			return null;

		giverScratch.Clear();
		List<JoyGiverDef> all = DefDatabase<JoyGiverDef>.AllDefsListForReading;
		for (int i = 0; i < all.Count; i++)
		{
			JoyGiverDef def = all[i];
			if (def?.Worker == null || def.jobDef == null || def.joyKind == null)
				continue;
			// Building games (chess, poker, billiards, TV, …) — skip pure social/skygaze etc.
			if (def.thingDefs == null || def.thingDefs.Count == 0)
				continue;
			if (!def.Worker.CanBeGivenTo(pawn))
				continue;
			giverScratch.Add(def);
		}

		if (giverScratch.Count == 0)
			return null;

		for (int n = giverScratch.Count; n > 1; n--)
		{
			int j = Rand.Range(0, n);
			(giverScratch[j], giverScratch[n - 1]) = (giverScratch[n - 1], giverScratch[j]);
		}
		for (int i = 0; i < giverScratch.Count; i++)
		{
			Job? job = giverScratch[i].Worker.TryGiveJob(pawn);
			if (job == null)
				continue;
			Thing? target = job.targetA.Thing;
			if (target == null)
				continue;
			Room? room = target.GetRoom();
			bool inCommons = room != null && PrisonCommons.IsLingerCommons(room);
			bool markedJoy = PrisonCommons.IsPrisonerOnlyJoyBuilding(target);
			if (!inCommons && !markedJoy)
				continue;
			return job;
		}

		return null;
	}

	/// <summary>
	/// Must try play while already in commons. ShouldPreferLingerCommons is false there
	/// (already near marker), so gating only on that made them never sit at chess after arriving.
	/// </summary>
	public static bool ShouldTryPlay(Pawn pawn)
	{
		if (pawn is not { IsPrisoner: true, Spawned: true })
			return false;
		if (PrisonCommons.ShouldVacateLeaveArea(pawn))
			return false;
		if (!PrisonCommons.IsDaytimeForHangout(pawn))
			return false;
		if (pawn.needs?.rest != null && pawn.needs.rest.CurLevel < Need_Rest.ThreshTired)
			return false;
		if (pawn.Downed || pawn.InMentalState)
			return false;

		if (PrisonCommons.IsNearActiveLingerMarker(pawn))
			return Rand.Chance(TryPlayChanceInCommons);

		return PrisonCommons.ShouldPreferLingerCommons(pawn) && Rand.Chance(TryPlayChanceEnRoute);
	}

	public static void NotifyPlayFinished(Pawn pawn, JoyKindDef? joyKind = null)
	{
		if (pawn?.needs?.mood?.thoughts?.memories == null)
			return;

		ThoughtDef? def = ResolveThought(joyKind);
		if (def == null)
			return;
		pawn.needs.mood.thoughts.memories.TryGainMemory(def);
		PrisonCommonsLog.Decision(pawn, "RECREATE_DONE",
			$"gained {def.defName} (kind={joyKind?.defName ?? "generic"})");
	}

	private static ThoughtDef? ResolveThought(JoyKindDef? joyKind)
	{
		if (joyKind != null)
		{
			ThoughtDef? typed = DefDatabase<ThoughtDef>.GetNamedSilentFail(
				"PrisonCommons_Recreated_" + joyKind.defName);
			if (typed != null)
				return typed;
		}
		return DefDatabase<ThoughtDef>.GetNamedSilentFail("PrisonCommons_Recreated");
	}
}
