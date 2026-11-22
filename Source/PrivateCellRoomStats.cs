using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonCommons;

/// <summary>
/// Private prison cells stay separate rooms, but Beauty/Space/Wealth/Cleanliness can
/// inherit from a directly adjacent linger commons (through a cell door or cell bars).
/// </summary>
internal static class PrivateCellRoomStats
{
	private static readonly HashSet<string> CellBarDefNames = new()
	{
		"RB_CellWall",
		"RB_BarriersWall"
	};

	public static bool ShouldBorrow(Room? room)
	{
		if (room == null || room.TouchesMapEdge || !room.IsPrisonCell)
			return false;
		// Commons themselves are marked IsPrisonCell for door logic — don't borrow from self.
		if (PrisonCommons.IsLingerCommons(room))
			return false;
		return true;
	}

	public static bool TryGetLinkedLingerCommons(Room privateCell, out Room commons)
	{
		commons = null!;
		Map? map = privateCell.Map;
		if (map == null)
			return false;

		foreach (IntVec3 border in privateCell.BorderCellsCached)
		{
			for (int i = 0; i < 4; i++)
			{
				IntVec3 step = GenAdj.CardinalDirections[i];
				IntVec3 n = border + step;
				if (!n.InBounds(map))
					continue;

				if (TryRoomFromNeighborCell(n, step, map, privateCell, out commons))
					return true;
			}
		}

		return false;
	}

	public static float Borrow(float own, float fromCommons)
		=> Mathf.Max(own, fromCommons);

	private static bool TryRoomFromNeighborCell(IntVec3 n, IntVec3 step, Map map, Room privateCell, out Room commons)
	{
		commons = null!;

		Building_Door? door = n.GetDoor(map);
		if (door != null)
		{
			Room? doorRoom = n.GetRoom(map);
			if (doorRoom != null && TryCommonsBeyondDoorway(doorRoom, privateCell, out commons))
				return true;
		}

		if (IsCellBar(n.GetEdifice(map)))
		{
			IntVec3 beyond = n + step;
			if (beyond.InBounds(map))
			{
				Room? beyondRoom = beyond.GetRoom(map);
				if (beyondRoom != null && beyondRoom != privateCell && PrisonCommons.IsLingerCommons(beyondRoom))
				{
					commons = beyondRoom;
					return true;
				}
			}
		}

		Room? direct = n.GetRoom(map);
		if (direct != null && direct != privateCell && PrisonCommons.IsLingerCommons(direct))
		{
			commons = direct;
			return true;
		}

		return false;
	}

	private static bool TryCommonsBeyondDoorway(Room doorway, Room privateCell, out Room commons)
	{
		commons = null!;
		if (!doorway.IsDoorway)
			return false;

		List<Region>? regions = doorway.Regions;
		if (regions == null)
			return false;

		for (int i = 0; i < regions.Count; i++)
		{
			Region? region = regions[i];
			if (region?.Neighbors == null)
				continue;

			foreach (Region neighbor in region.Neighbors)
			{
				Room? room = neighbor?.Room;
				if (room == null || room == privateCell || room == doorway)
					continue;
				if (PrisonCommons.IsLingerCommons(room))
				{
					commons = room;
					return true;
				}
			}
		}

		return false;
	}

	private static bool IsCellBar(Building? edifice)
	{
		if (edifice?.def == null)
			return false;
		return CellBarDefNames.Contains(edifice.def.defName);
	}
}
