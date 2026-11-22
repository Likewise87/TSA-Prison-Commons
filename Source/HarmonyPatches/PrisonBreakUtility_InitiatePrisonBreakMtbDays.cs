using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonCommons;

/// <summary>
/// Replaces prison-break MTB so tip math always fills (even when the interval is Never),
/// respects ignoreAsleep for the Prisoner tab, merges doors across IsPrisonCell rooms,
/// and applies private-cell interval factor (x250%).
/// </summary>
[HarmonyPatch(typeof(PrisonBreakUtility), nameof(PrisonBreakUtility.InitiatePrisonBreakMtbDays))]
[HarmonyPriority(Priority.First)]
internal static class PrisonBreakUtility_InitiatePrisonBreakMtbDays
{
	internal const float PrivateCellMtbFactor = 2.5f;

	/// <summary>Owned-bed room must have this many prisoner sleeping slots or fewer (at least 1).</summary>
	internal const int MaxPrivateCellSleepingSlots = 2;

	private static readonly SimpleCurve? DaysSinceBreakCurve =
		AccessTools.StaticFieldRefAccess<SimpleCurve>(
			typeof(PrisonBreakUtility),
			"PrisonBreakMTBFactorForDaysSincePrisonBreak");

	private static readonly HashSet<Region> TmpRegions = [];
	private static readonly HashSet<Building_Door> TmpDoors = [];

	static bool Prefix(ref float __result, Pawn pawn, StringBuilder? sb, bool ignoreAsleep)
	{
		if (pawn == null)
		{
			__result = -1f;
			return false;
		}

		if (sb != null)
			sb.Length = 0;

		string? neverReason = null;
		if (!ignoreAsleep && !pawn.Awake())
			neverReason = "TSA_PrisonBreak_NeverAsleep";
		else if (!PrisonBreakUtility.CanParticipateInPrisonBreak(pawn))
			neverReason = NeverParticipateReason(pawn);

		Room? room = pawn.GetRoom();
		if (neverReason == null && room is not { IsPrisonCell: true })
			neverReason = "TSA_PrisonBreak_NeverNotPrison";

		// Tip math can use owned bed room when the pawn is elsewhere / Never.
		Room? mathRoom = room is { IsPrisonCell: true }
			? room
			: pawn.ownership?.OwnedBed?.GetRoom();
		if (mathRoom is not { IsPrisonCell: true })
			mathRoom = null;

		float days = pawn.kindDef.basePrisonBreakMtbDays;
		if (mathRoom != null)
			ApplyFactors(pawn, mathRoom, ref days, sb, writeTip: sb != null);
		else if (sb != null)
		{
			sb.AppendLine("TSA_PrisonBreak_Base".Translate(FormatDays(pawn.kindDef.basePrisonBreakMtbDays)));
			if (IsPrivatePrisonSleepingCell(pawn))
				AppendPrivateCellFactorLine(sb);
		}

		if (neverReason != null)
		{
			__result = -1f;
			if (sb != null)
			{
				string breakdown = sb.ToString();
				sb.Length = 0;
				sb.AppendLine("TSA_PrisonBreak_NeverHeader".Translate() + ": " + neverReason.Translate());
				if (!breakdown.NullOrEmpty())
				{
					sb.AppendLine();
					sb.Append(breakdown.TrimEnd());
					sb.AppendLine();
					sb.Append("TSA_PrisonBreak_WouldBe".Translate(FormatDays(days)));
				}
			}

			return false;
		}

		__result = days;
		if (sb != null)
			sb.Append("TSA_PrisonBreak_Result".Translate(FormatDays(days)));
		return false;
	}

	private static string FormatDays(float days) => days.ToString("0.#");

	private static string NeverParticipateReason(Pawn pawn)
	{
		if (pawn.Downed)
			return "TSA_PrisonBreak_NeverDowned";
		if (PrisonBreakUtility.IsPrisonBreaking(pawn))
			return "TSA_PrisonBreak_NeverAlreadyBreaking";
		if (PrisonBreakUtility.GenePreventsPrisonBreaking(pawn, out _))
			return "TSA_PrisonBreak_NeverGene";
		return "TSA_PrisonBreak_NeverCannot";
	}

	private static void ApplyFactors(Pawn pawn, Room room, ref float days, StringBuilder? sb, bool writeTip)
	{
		float baseDays = days;
		if (writeTip && sb != null)
			sb.AppendLine("TSA_PrisonBreak_Base".Translate(FormatDays(baseDays)));

		float movement = Mathf.Clamp(pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving), 0.01f, 1f);
		days /= movement;
		if (writeTip && sb != null)
		{
			sb.AppendLine("  - " + "FactorForMovement".Translate() + ": x" +
			              (1f / movement).ToStringPercent());
		}

		float doorCount = CountEscapeDoorsCombined(room);
		if (doorCount > 0f)
		{
			days /= doorCount;
			if (writeTip && sb != null)
			{
				sb.AppendLine("  - " + "FactorForDoorCount".Translate() + ": x" +
				              (1f / doorCount).ToStringPercent() +
				              " (" + "TSA_PrisonBreak_DoorCount".Translate(((int)doorCount).ToString()) + ")");
			}
		}
		else if (writeTip && sb != null)
		{
			sb.AppendLine("  - " + "FactorForDoorCount".Translate() + ": x" +
			              1f.ToStringPercent() + " (" + "TSA_PrisonBreak_DoorCount".Translate("0") + ")");
		}

		if (ModsConfig.AnomalyActive && TmpDoors.Count > 0)
		{
			bool anySecurity = false;
			bool allSecurity = true;
			foreach (Building_Door door in TmpDoors)
			{
				if (door?.def == ThingDefOf.SecurityDoor)
					anySecurity = true;
				else
					allSecurity = false;
			}

			if (anySecurity && allSecurity)
			{
				days *= 1.1f;
				if (writeTip && sb != null)
					sb.AppendLine("  - " + "SecurityDoorsOnly".Translate() + ": x" + 1.1f.ToStringPercent());
			}
		}

		if (pawn.genes != null)
		{
			float geneFactor = pawn.genes.PrisonBreakIntervalFactor;
			days *= geneFactor;
			if (writeTip && sb != null)
				sb.AppendLine("  - " + "FactorForGenes".Translate() + ": x" + geneFactor.ToStringPercent());
		}

		if (ModsConfig.AnomalyActive && pawn.health.hediffSet.HasHediff(HediffDefOf.BlissLobotomy))
		{
			days *= 10f;
			if (writeTip && sb != null)
				sb.AppendLine("  - " + "BlissLobotomy".Translate() + ": x" + 10f.ToStringPercent());
		}

		if (pawn.guest != null && pawn.guest.everParticipatedInPrisonBreak && DaysSinceBreakCurve != null)
		{
			float daysSince =
				(Find.TickManager.TicksGame - pawn.guest.lastPrisonBreakTicks) / (float)GenDate.TicksPerDay;
			float curveFactor = DaysSinceBreakCurve.Evaluate(daysSince);
			days *= curveFactor;
			if (writeTip && sb != null)
			{
				sb.AppendLine("  - " + "TSA_PrisonBreak_FactorRecentBreak".Translate() + ": x" +
				              curveFactor.ToStringPercent());
			}
		}

		if (IsPrivatePrisonSleepingCell(pawn))
		{
			days *= PrivateCellMtbFactor;
			if (writeTip && sb != null)
				AppendPrivateCellFactorLine(sb);
		}
	}

	private static void AppendPrivateCellFactorLine(StringBuilder sb)
	{
		sb.AppendLine("  - " + "TSA_PrivatePrisonCell_Label".Translate() + ": x" +
		              PrivateCellMtbFactor.ToStringPercent());
	}

	private static float CountEscapeDoorsCombined(Room room)
	{
		TmpRegions.Clear();
		TmpDoors.Clear();
		CountEscapeDoorsRecursive(room);
		return TmpDoors.Count;
	}

	private static void CountEscapeDoorsRecursive(Room room)
	{
		List<Region> regions = room.Regions;
		var snap = new Region[regions.Count];
		for (int i = 0; i < regions.Count; i++)
			snap[i] = regions[i];

		for (int r = 0; r < snap.Length; r++)
		{
			Region region = snap[r];
			if (region == null || !TmpRegions.Add(region))
				continue;

			if (region.links == null)
				continue;

			for (int l = 0; l < region.links.Count; l++)
			{
				Region other = region.links[l].GetOtherRegion(region);
				if (other == null || other.type != RegionType.Portal || !TmpRegions.Add(other))
					continue;

				Building_Door? door = other.door;
				foreach (Region neighbor in other.Neighbors)
				{
					Room? neighborRoom = neighbor?.Room;
					if (neighborRoom != null && neighborRoom.IsPrisonCell)
						CountEscapeDoorsRecursive(neighborRoom);
					else if (door != null)
						TmpDoors.Add(door);
				}
			}
		}
	}

	internal static bool IsPrivatePrisonSleepingCell(Pawn pawn)
		=> TryExplainPrivatePrisonSleepingCell(pawn, out _) == true;

	internal static bool? TryExplainPrivatePrisonSleepingCell(Pawn pawn, out string reasonKey)
	{
		reasonKey = string.Empty;
		if (pawn == null || !pawn.IsPrisonerOfColony)
			return null;

		Building_Bed? bed = pawn.ownership?.OwnedBed;
		if (bed == null || !bed.Spawned)
		{
			reasonKey = "TSA_PrivatePrisonCell_Fail_NoOwnedBed";
			return false;
		}

		Room? room = bed.GetRoom();

		// Prefer RimWorld room math when the cell is actually a separate non-commons room.
		if (room is { IsPrisonCell: true } && !PrisonCommons.IsLingerCommons(room))
		{
			int slots = CountPrisonerSleepingSlots(room);
			if (slots >= 1 && slots <= MaxPrivateCellSleepingSlots)
				return true;
			reasonKey = "TSA_PrivatePrisonCell_Fail_NotSingleSlot";
			return false;
		}

		// Bar walls can wrongly merge cell+commons into one Room (IFE). Fall back to a flood
		// that stops at cell doors / closesRooms walls so private alcoves still count.
		if (PrivateCellFlood.TryCountSleepingSlots(bed, out int floodSlots, out bool touchesLingerMarker))
		{
			if (touchesLingerMarker)
			{
				reasonKey = "TSA_PrivatePrisonCell_Fail_Commons";
				return false;
			}

			if (floodSlots >= 1 && floodSlots <= MaxPrivateCellSleepingSlots)
				return true;

			reasonKey = "TSA_PrivatePrisonCell_Fail_NotSingleSlot";
			return false;
		}

		if (room is not { IsPrisonCell: true })
		{
			reasonKey = "TSA_PrivatePrisonCell_Fail_NotPrisonCell";
			return false;
		}

		reasonKey = "TSA_PrivatePrisonCell_Fail_Commons";
		return false;
	}

	internal static string GetInspectStatusLine(Pawn pawn)
	{
		if (!IsPrivatePrisonSleepingCell(pawn))
			return string.Empty;

		Room? room = pawn.GetRoom();
		if (room is not { IsPrisonCell: true })
			room = pawn.ownership?.OwnedBed?.GetRoom();

		float days = pawn.kindDef.basePrisonBreakMtbDays;
		if (room is { IsPrisonCell: true })
			ApplyFactors(pawn, room, ref days, null, writeTip: false);
		else
			days *= PrivateCellMtbFactor;

		bool never = days <= 0f || !PrisonBreakUtility.CanParticipateInPrisonBreak(pawn);
		string interval = never
			? "TSA_PrisonBreak_NeverHeader".Translate()
			: "TSA_PrivatePrisonCell_InspectDays".Translate(FormatDays(days));
		return "TSA_PrivatePrisonCell_InspectOk".Translate(interval);
	}

	private static int CountPrisonerSleepingSlots(Room room)
	{
		Map? map = room.Map;
		if (map == null)
			return 0;

		int slots = 0;
		var seenBeds = new HashSet<Building_Bed>();
		foreach (IntVec3 cell in room.Cells)
		{
			List<Thing> things = cell.GetThingList(map);
			for (int i = 0; i < things.Count; i++)
			{
				if (things[i] is not Building_Bed bed || !bed.ForPrisoners || !seenBeds.Add(bed))
					continue;
				slots += bed.SleepingSlotsCount;
			}
		}

		return slots;
	}
}
