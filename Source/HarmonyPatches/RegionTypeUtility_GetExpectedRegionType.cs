using HarmonyLib;
using RimWorld;
using Verse;

namespace PrisonCommons;

/// <summary>
/// ReBuild cell walls are bar walls: vanilla marks them ImpassableFreeAirExchange, which
/// merges into the same Room as Normal cells. ReBuild's closesRooms postfix only runs when
/// the result is already IFE — reinforce at Priority.Last so bars actually seal private cells.
/// Never touches Portal (doors) — that previously destroyed the region graph.
/// </summary>
[HarmonyPatch(typeof(RegionTypeUtility), nameof(RegionTypeUtility.GetExpectedRegionType))]
[HarmonyPriority(Priority.Last)]
internal static class RegionTypeUtility_GetExpectedRegionType
{
	public static void Postfix(IntVec3 c, Map map, ref RegionType __result)
	{
		if (map == null)
			return;

		// Doors must stay Portal. Never rewrite Portal/None/Normal walkable.
		if (__result == RegionType.Portal || __result == RegionType.None || __result == RegionType.Normal)
			return;

		if (__result != RegionType.ImpassableFreeAirExchange && __result != RegionType.Fence)
			return;

		// Prefer edifice (wall); skip if this cell is a door.
		if (c.GetDoor(map) != null)
			return;

		var edifice = c.GetEdifice(map);
		if (edifice?.def == null)
			return;

		if (!ClosesRoomsForPrison(edifice.def))
			return;

		__result = RegionType.None;
	}

	private static bool ClosesRoomsForPrison(ThingDef def)
	{
		if (def.defName is "RB_CellWall" or "RB_BarriersWall")
			return true;

		var extensions = def.modExtensions;
		if (extensions == null)
			return false;

		for (var i = 0; i < extensions.Count; i++)
		{
			var ext = extensions[i];
			if (ext == null)
				continue;
			if (ext.GetType().FullName != "ReBuildDoorsAndCorners.ThingExtension")
				continue;

			var field = AccessTools.Field(ext.GetType(), "closesRooms");
			if (field != null && field.GetValue(ext) is true)
				return true;
		}

		return false;
	}
}
