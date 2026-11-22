using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;

namespace PrisonCommons;

/// <summary>
/// Decision-only logging. Enable in mod settings. Filter Player.log for [PrisonCommons].
/// </summary>
internal static class PrisonCommonsLog
{
	private static readonly Dictionary<int, int> lastTickByKey = new();

	public static bool Enabled
		=> PrisonCommonsMod.Settings?.enableLogging == true;

	public static void Message(string message)
	{
		if (!Enabled)
			return;
		Log.Message("[PrisonCommons] " + message);
	}

	public static void MessageThrottled(string key, string message, int intervalTicks = 600)
	{
		if (!Enabled)
			return;

		var tick = Find.TickManager?.TicksGame ?? 0;
		var hash = key.GetHashCode();
		if (lastTickByKey.TryGetValue(hash, out var last) && tick - last < intervalTicks)
			return;

		lastTickByKey[hash] = tick;
		Log.Message("[PrisonCommons] " + message);
	}

	/// <summary>One line per decision type per pawn; skips repeats within the interval.</summary>
	public static void Decision(Pawn pawn, string code, string detail, int intervalTicks = 900)
	{
		MessageThrottled(
			$"dec.{pawn.thingIDNumber}.{code}",
			$"{code} | {PawnLabel(pawn)} | {detail}",
			intervalTicks);
	}

	public static string PawnLabel(Pawn? pawn)
		=> pawn == null ? "(null)" : $"{pawn.LabelShortCap}#{pawn.thingIDNumber}";

	public static string RoomLabel(Room? room)
	{
		if (room == null)
			return "no-room";
		if (room.IsDoorway)
		{
			foreach (var c in room.Cells)
				return $"doorway@{c}";
			return "doorway";
		}

		var role = room.Role?.defName ?? "Unknown";
		return $"{role} cells={room.CellCount} prisonCell={room.IsPrisonCell}";
	}

	public static string JobLabel(Job? job)
	{
		if (job == null)
			return "none";
		var target = job.targetA.IsValid ? $" -> {job.targetA}" : "";
		return $"{job.def?.defName}{target}";
	}

	/// <summary>Compact hangout/reach snapshot for FAIL lines.</summary>
	public static string HangoutSnapshot(Pawn pawn)
	{
		var sb = new StringBuilder();
		var room = pawn.GetRoom();
		sb.Append("room=").Append(RoomLabel(room));
		sb.Append(" day=").Append(PrisonCommons.IsDaytimeForHangout(pawn));
		sb.Append(" inCommons=").Append(PrisonCommons.IsNearActiveLingerMarker(pawn));

		var map = pawn.Map;
		if (map == null)
			return sb.ToString();

		CompPrisonCommons? firstMarker = null;
		var markerCount = 0;
		foreach (var comp in PrisonCommons.ActiveComps)
		{
			if (!comp.parent.Spawned || comp.parent.Map != map)
				continue;
			if (!PrisonCommons.IsLingerMarker(comp))
				continue;
			markerCount++;
			firstMarker ??= comp;
		}

		sb.Append(" markers=").Append(markerCount);
		if (firstMarker != null)
		{
			var markerCell = firstMarker.parent.Position;
			var markerRoom = firstMarker.parent.GetRoom();
			sb.Append(" firstMarker@").Append(markerCell);
			sb.Append(" markerRoom=").Append(RoomLabel(markerRoom));
			sb.Append(" canReachMarker=").Append(
				pawn.CanReach(markerCell, PathEndMode.OnCell, Danger.Some));
		}

		var cellDoors = 0;
		var cellDoorsOpenable = 0;
		foreach (var door in map.listerBuildings.AllBuildingsColonistOfClass<Building_Door>())
		{
			if (!CellDoorPass.IsPrisonCellDoor(door))
				continue;
			cellDoors++;
			if (door.PawnCanOpen(pawn))
				cellDoorsOpenable++;
		}

		sb.Append(" cellDoors=").Append(cellDoors);
		sb.Append(" cellDoorsOpenable=").Append(cellDoorsOpenable);

		var bed = pawn.ownership?.OwnedBed;
		if (bed != null)
		{
			sb.Append(" privateCell=")
				.Append(PrisonBreakUtility_InitiatePrisonBreakMtbDays.IsPrivatePrisonSleepingCell(pawn));
			sb.Append(" bedRoom=").Append(RoomLabel(bed.GetRoom()));
		}

		return sb.ToString();
	}
}
