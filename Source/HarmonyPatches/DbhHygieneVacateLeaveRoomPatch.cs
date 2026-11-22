using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace PrisonCommons;

/// <summary>
/// After DBH hygiene finishes in a leave bathroom, queue Goto to linger commons (else bed).
/// Skips vacate when UseToilet just queued washHands in the same room.
/// Patches JobDriver.Cleanup once (DBH drivers do not override Cleanup) and filters by type.
/// Soft-resolves DBH types so the mod loads without Dubs Bad Hygiene.
/// </summary>
[HarmonyPatch(typeof(JobDriver), nameof(JobDriver.Cleanup))]
internal static class DbhHygieneVacateLeaveRoomPatch
{
	private static readonly string[] VacateDriverTypeNames =
	[
		"DubsBadHygiene.JobDriver_washHands",
		"DubsBadHygiene.JobDriver_takeShower",
		"DubsBadHygiene.JobDriver_takeBath",
		"DubsBadHygiene.JobDriver_useWashBucket",
		"DubsBadHygiene.JobDriver_washAtCell",
		"DubsBadHygiene.JobDriver_UseToilet"
	];

	private static readonly HashSet<Type> VacateDriverTypes = [];

	static bool Prepare()
	{
		VacateDriverTypes.Clear();
		foreach (var name in VacateDriverTypeNames)
		{
			var type = AccessTools.TypeByName(name);
			if (type != null)
				VacateDriverTypes.Add(type);
		}

		return VacateDriverTypes.Count > 0;
	}

	static void Postfix(JobDriver __instance, JobCondition condition)
	{
		if (condition != JobCondition.Succeeded)
			return;

		if (__instance == null || !VacateDriverTypes.Contains(__instance.GetType()))
			return;

		var pawn = __instance.pawn;
		if (pawn is not { IsPrisoner: true, Spawned: true })
			return;

		if (!PrisonCommons.ShouldVacateLeaveArea(pawn))
			return;

		if (__instance.GetType().Name == "JobDriver_UseToilet" && HasPendingWashHands(pawn))
			return;

		PrisonCommons.TryEnqueueVacateLeaveRoom(pawn);
	}

	private static bool HasPendingWashHands(Pawn pawn)
	{
		var queue = pawn.jobs?.jobQueue;
		if (queue == null)
			return false;

		for (var i = 0; i < queue.Count; i++)
		{
			var queued = queue[i].job;
			if (queued?.def == null)
				continue;
			if (IsWashHandsDef(queued.def.defName))
				return true;
		}

		var cur = pawn.CurJob?.def?.defName;
		return cur != null && IsWashHandsDef(cur);
	}

	private static bool IsWashHandsDef(string defName)
		=> defName.IndexOf("washHands", StringComparison.OrdinalIgnoreCase) >= 0;
}
