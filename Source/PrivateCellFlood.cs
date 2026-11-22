using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PrisonCommons;

/// <summary>
/// Counts prisoner beds in the sealed alcove around an owned bed, stopping at cell doors
/// and closesRooms bar walls. Used when RimWorld merges cell+commons through IFE bars.
/// </summary>
internal static class PrivateCellFlood
{
	private const int MaxCells = 64;

	public static bool TryCountSleepingSlots(Building_Bed bed, out int slots, out bool touchesLingerMarker)
	{
		slots = 0;
		touchesLingerMarker = false;
		var map = bed.Map;
		if (map == null || !bed.Spawned)
			return false;

		var visited = new HashSet<IntVec3>();
		var queue = new Queue<IntVec3>();
		var seenBeds = new HashSet<Building_Bed>();

		foreach (var c in bed.OccupiedRect())
		{
			if (visited.Add(c))
				queue.Enqueue(c);
		}

		while (queue.Count > 0 && visited.Count < MaxCells)
		{
			var c = queue.Dequeue();
			var things = map.thingGrid.ThingsListAtFast(c);
			for (var i = 0; i < things.Count; i++)
			{
				var t = things[i];
				if (t is Building_Bed prisonerBed && prisonerBed.ForPrisoners && seenBeds.Add(prisonerBed))
					slots += prisonerBed.SleepingSlotsCount;

				var commons = t.TryGetComp<CompPrisonCommons>();
				if (commons != null && PrisonCommons.IsLingerMarker(commons) &&
				    PrisonCommons.IsRegisteredActiveMarker(commons))
				{
					touchesLingerMarker = true;
				}
			}

			foreach (var dir in GenAdj.CardinalDirections)
			{
				var n = c + dir;
				if (!n.InBounds(map) || !visited.Add(n))
					continue;
				if (IsBarrier(n, map))
					continue;
				queue.Enqueue(n);
			}
		}

		return true;
	}

	private static bool IsBarrier(IntVec3 c, Map map)
	{
		// Any door ends the private alcove (cell door to commons / hall).
		if (c.GetDoor(map) != null)
			return true;

		var edifice = c.GetEdifice(map);
		if (edifice == null)
			return !c.Walkable(map);

		if (edifice.def.defName is "RB_CellWall" or "RB_BarriersWall")
			return true;

		var extensions = edifice.def.modExtensions;
		if (extensions != null)
		{
			for (var i = 0; i < extensions.Count; i++)
			{
				var ext = extensions[i];
				if (ext?.GetType().FullName != "ReBuildDoorsAndCorners.ThingExtension")
					continue;
				var field = HarmonyLib.AccessTools.Field(ext.GetType(), "closesRooms");
				if (field != null && field.GetValue(ext) is true)
					return true;
			}
		}

		return edifice.def.passability == Traversability.Impassable;
	}
}
