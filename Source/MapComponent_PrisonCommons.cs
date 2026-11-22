using Verse;

namespace PrisonCommons;

/// <summary>
/// After load, rebuild regions once so cell-wall closesRooms (IFE→None) splits private cells.
/// Rare tick: vent heat/gas through sealed cell bars without merging rooms.
/// </summary>
public class MapComponent_PrisonCommons : MapComponent
{
	private const int AirEqualizeInterval = 250;

	private bool rebuilt;

	public MapComponent_PrisonCommons(Map map) : base(map)
	{
	}

	public override void FinalizeInit()
	{
		base.FinalizeInit();
		TryRebuildOnce();
	}

	public override void MapComponentTick()
	{
		// Mid-session mod reload: FinalizeInit may already have run before our patch existed.
		if (!rebuilt && Find.TickManager.TicksGame > 1)
			TryRebuildOnce();

		if (map.IsHashIntervalTick(AirEqualizeInterval))
			CellAirExchange.EqualizeOnMap(map);
	}

	private void TryRebuildOnce()
	{
		if (rebuilt || map?.regionAndRoomUpdater == null)
			return;

		rebuilt = true;
		map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
		PrisonCommonsLog.Message("Rebuilt regions so cell walls seal private cells (IFE→None).");
	}
}
