using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PrisonCommons;

/// <summary>
/// Cell bars stay room-sealing (RegionType.None) for prison logic, but vent heat/gas
/// across the barrier so private cells equalize with the commons.
/// </summary>
internal static class CellAirExchange
{
	/// <summary>Same order of magnitude as <see cref="Building_Vent"/> (14).</summary>
	private const float EqualizeRate = 12f;

	private static readonly string[] BarrierDefNames =
	{
		"RB_CellWall",
		"RB_BarriersWall"
	};

	private static List<ThingDef>? barrierDefs;
	private static bool defsResolved;

	public static void EqualizeOnMap(Map map)
	{
		if (map?.listerBuildings == null)
			return;

		EnsureDefs();
		if (barrierDefs == null || barrierDefs.Count == 0)
			return;

		for (int i = 0; i < barrierDefs.Count; i++)
		{
			ThingDef def = barrierDefs[i];
			List<Building> buildings = map.listerBuildings.AllBuildingsColonistOfDef(def);
			for (int b = 0; b < buildings.Count; b++)
			{
				Building building = buildings[b];
				if (building == null || !building.Spawned)
					continue;

				GenTemperature.EqualizeTemperaturesThroughBuilding(building, EqualizeRate, twoWay: false);
				map.gasGrid?.EqualizeGasThroughBuilding(building, twoWay: false);
			}
		}
	}

	private static void EnsureDefs()
	{
		if (defsResolved)
			return;
		defsResolved = true;
		barrierDefs = new List<ThingDef>(BarrierDefNames.Length);
		for (int i = 0; i < BarrierDefNames.Length; i++)
		{
			ThingDef? def = DefDatabase<ThingDef>.GetNamedSilentFail(BarrierDefNames[i]);
			if (def != null)
				barrierDefs.Add(def);
		}
	}
}
