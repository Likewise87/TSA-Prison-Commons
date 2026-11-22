using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PrisonCommons;

[StaticConstructorOnStartup]
public static class PrisonCommons
{
	private static readonly HashSet<CompPrisonCommons> comps = [];

	/// <summary>Active Prison Commons markers (for diagnostics).</summary>
	internal static IReadOnlyCollection<CompPrisonCommons> ActiveComps => comps;

	internal static bool IsRegisteredActiveMarker(CompPrisonCommons comp)
		=> comps.Contains(comp);

	static PrisonCommons()
	{
		new Harmony("TSA.PrisonCommons").PatchAll(Assembly.GetExecutingAssembly());
	}

	/// <summary>
	/// Gather-spot / party / sculpture commons: hang out (IsPrisonCell, social, no escort).
	/// </summary>
	public static bool IsLingerCommons(Room? room)
		=> RoomHasActiveMarker(room, lingerOnly: true);

	/// <summary>
	/// DBH fixture commons with no linger marker in the room: door access only.
	/// </summary>
	public static bool IsLeaveCommons(Room? room)
	{
		if (room == null || room.TouchesMapEdge)
			return false;
		if (IsLingerCommons(room))
			return false;
		return RoomHasActiveMarker(room, lingerOnly: false);
	}

	/// <summary>Either linger or leave commons — used for door reachability.</summary>
	public static bool IsAccessCommons(Room? room)
		=> IsLingerCommons(room) || IsLeaveCommons(room);

	/// <summary>Backward-compatible name for hangout commons (linger only).</summary>
	public static bool IsPrisonCommons(Room? room)
		=> IsLingerCommons(room);

	/// <summary>
	/// Allowed doorway that touches a leave bathroom — idle here should vacate like the bathroom.
	/// </summary>
	public static bool IsLeaveTransit(Pawn? p, Room? room)
	{
		if (p == null || room is not { IsDoorway: true })
			return false;
		if (!IsAllowedDoorway(p, room))
			return false;
		return DoorwayTouchesLeaveCommons(room);
	}

	public static bool ShouldVacateLeaveArea(Pawn? p)
	{
		if (p == null)
			return false;
		var room = p.GetRoom();
		return IsLeaveCommons(room) || IsLeaveTransit(p, room);
	}

	/// <summary>
	/// Gather spots, party spots, sculptures, and tables (surfaceType Eat) are hangout markers.
	/// DBH BasedFixture (and anything else with this comp) is access-only when toggled.
	/// </summary>
	internal static bool IsLingerMarker(CompPrisonCommons comp)
	{
		var thing = comp.parent;
		if (thing.TryGetComp<CompGatherSpot>() != null)
			return true;
		if (thing.def.defName == "PartySpot")
			return true;
		if (thing.TryGetComp<CompArt>() != null)
			return true;
		if (thing.def.IsTable)
			return true;
		// Chess / TV / billiards / etc. marked For prisoners = hangout + prison-only use.
		if (IsJoyBuilding(thing.def))
			return true;
		return false;
	}

	internal static bool IsJoyBuilding(ThingDef? def)
	{
		if (def == null)
			return false;
		if (def.thingCategories != null)
		{
			for (int i = 0; i < def.thingCategories.Count; i++)
			{
				ThingCategoryDef? cat = def.thingCategories[i];
				if (cat != null && cat.defName == "BuildingsJoy")
					return true;
			}
		}
		return def.StatBaseDefined(StatDefOf.JoyGainFactor);
	}

	/// <summary>Active Prison Commons on this building → prisoners may use; colonists may not.</summary>
	internal static bool IsPrisonerOnlyJoyBuilding(Thing? t)
	{
		if (t == null || !IsJoyBuilding(t.def))
			return false;
		var comp = t.TryGetComp<CompPrisonCommons>();
		return comp != null && comps.Contains(comp);
	}

	internal static bool IsHygieneAccessMarker(CompPrisonCommons comp)
		=> !IsLingerMarker(comp);

	public static bool IsHygieneThing(Thing? t)
	{
		if (t == null)
			return false;
		var comp = t.TryGetComp<CompPrisonCommons>();
		return comp != null && IsHygieneAccessMarker(comp);
	}

	private static bool RoomHasActiveMarker(Room? room, bool lingerOnly)
	{
		if (room == null || room.TouchesMapEdge)
			return false;

		foreach (var comp in comps)
		{
			if (!comp.parent.Spawned || comp.parent.Map != room.Map)
				continue;
			if (room != comp.parent.GetRoom())
				continue;

			var linger = IsLingerMarker(comp);
			if (lingerOnly)
			{
				if (linger)
					return true;
			}
			else if (!linger)
			{
				return true;
			}
		}

		return false;
	}

	internal static void SetPrisonCommons(CompPrisonCommons comp, bool add)
	{
		if (add)
			comps.Add(comp);
		else
			comps.Remove(comp);

		if (comp.parent.Spawned)
			comp.parent.GetRoom()?.Notify_RoomShapeChanged();
	}

	/// <summary>Local day percent window when idle prisoners prefer commons.</summary>
	private const float DayHangoutStart = 0.25f;
	private const float DayHangoutEnd = 0.80f;

	public static bool IsDaytimeForHangout(Pawn pawn)
	{
		var day = GenLocalDate.DayPercent(pawn);
		return day >= DayHangoutStart && day <= DayHangoutEnd;
	}

	/// <summary>Already hanging out = in a linger-commons room (no distance check).</summary>
	public static bool IsNearActiveLingerMarker(Pawn pawn)
		=> pawn is { Spawned: true } && IsLingerCommons(pawn.GetRoom());

	/// <summary>
	/// Prefer a standable hangout cell in a reachable linger commons room.
	/// </summary>
	public static bool TryFindLingerCommonsDest(Pawn pawn, out IntVec3 dest)
	{
		dest = IntVec3.Invalid;
		if (pawn?.Map == null || !pawn.Spawned)
			return false;

		return TryFindDistinctLingerRoomDest(pawn, out dest);
	}

	private static bool TryFindDistinctLingerRoomDest(Pawn pawn, out IntVec3 dest)
	{
		dest = IntVec3.Invalid;
		var map = pawn.Map;
		var currentRoom = pawn.GetRoom();
		var seenLingerRooms = new HashSet<Room>();

		foreach (var comp in comps)
		{
			if (!comp.parent.Spawned || comp.parent.Map != map)
				continue;
			if (!IsLingerMarker(comp))
				continue;

			var room = comp.parent.GetRoom();
			if (room == null || room == currentRoom || !IsLingerCommons(room) || !seenLingerRooms.Add(room))
				continue;

			var cells = room.Cells
				.Where(c => c.Standable(map) && pawn.CanReach(c, PathEndMode.OnCell, Danger.Some))
				.ToList();
			if (cells.Count > 0 && cells.TryRandomElement(out dest))
				return true;
		}

		return false;
	}

	/// <summary>
	/// Prefer a standable cell in a reachable linger commons; else owned bed.
	/// </summary>
	public static bool TryFindLeaveBathroomDest(Pawn pawn, out IntVec3 dest)
	{
		if (TryFindLingerCommonsDest(pawn, out dest))
			return true;

		dest = IntVec3.Invalid;
		if (pawn?.Map == null || !pawn.Spawned)
			return false;

		var bed = pawn.ownership?.OwnedBed;
		if (bed is { Spawned: true } && bed.Map == pawn.Map)
		{
			dest = bed.Position;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Daytime idle: hang out at commons (distinct room or near marker). Night/tired stay put.
	/// </summary>
	public static bool ShouldPreferLingerCommons(Pawn? pawn)
	{
		if (pawn is not { IsPrisoner: true, Spawned: true })
			return false;
		if (ShouldVacateLeaveArea(pawn))
			return false;
		if (!IsDaytimeForHangout(pawn))
			return false;
		if (pawn.needs?.rest != null && pawn.needs.rest.CurLevel < Need_Rest.ThreshTired)
			return false;
		if (IsNearActiveLingerMarker(pawn))
			return false;
		return TryFindLingerCommonsDest(pawn, out _);
	}

	/// <summary>Human-readable why prefer-linger succeeded or failed (for debug log).</summary>
	public static string DiagnosePreferLinger(Pawn pawn)
	{
		if (pawn is not { IsPrisoner: true, Spawned: true })
			return "not a spawned prisoner";

		var room = pawn.GetRoom();
		if (ShouldVacateLeaveArea(pawn))
			return $"vacate leave area instead (room={PrisonCommonsLog.RoomLabel(room)})";

		if (!IsDaytimeForHangout(pawn))
			return $"night/outside hangout window (dayPercent={GenLocalDate.DayPercent(pawn):0.00})";

		if (pawn.needs?.rest != null && pawn.needs.rest.CurLevel < Need_Rest.ThreshTired)
			return "rest too low (tired)";

		if (IsNearActiveLingerMarker(pawn))
			return $"already in linger commons (room={PrisonCommonsLog.RoomLabel(room)})";

		if (TryFindDistinctLingerRoomDest(pawn, out _))
			return "ok (distinct linger room reachable)";

		var map = pawn.Map;
		var lingerMarkers = 0;
		foreach (var comp in comps)
		{
			if (!comp.parent.Spawned || comp.parent.Map != map)
				continue;
			if (IsLingerMarker(comp))
				lingerMarkers++;
		}

		if (lingerMarkers == 0)
			return $"no active linger markers (registered comps={comps.Count})";

		var home = GetPrisonerHomeRoom(pawn);
		return
			$"linger markers={lingerMarkers} but no reachable hangout cell " +
			$"(home={PrisonCommonsLog.RoomLabel(home)}, current={PrisonCommonsLog.RoomLabel(room)})";
	}

	public static Job? MakePreferLingerCommonsJob(Pawn pawn)
	{
		if (!TryFindLingerCommonsDest(pawn, out var dest))
			return null;
		var job = JobMaker.MakeJob(JobDefOf.Goto, dest);
		job.locomotionUrgency = LocomotionUrgency.Walk;
		return job;
	}

	public static Job? MakeVacateLeaveRoomJob(Pawn pawn)
	{
		if (!TryFindLeaveBathroomDest(pawn, out var dest))
			return null;
		var job = JobMaker.MakeJob(JobDefOf.Goto, dest);
		job.locomotionUrgency = LocomotionUrgency.Walk;
		return job;
	}

	/// <summary>
	/// Queue a vacate Goto without interrupting an in-progress StartJob/Cleanup stack.
	/// </summary>
	public static void TryEnqueueVacateLeaveRoom(Pawn pawn)
	{
		if (pawn is not { Spawned: true, IsPrisoner: true })
			return;
		if (!ShouldVacateLeaveArea(pawn))
			return;

		var job = MakeVacateLeaveRoomJob(pawn);
		if (job == null)
		{
			PrisonCommonsLog.Decision(pawn, "VACATE_FAIL",
				$"enqueue no dest | {PrisonCommonsLog.HangoutSnapshot(pawn)}");
			return;
		}

		PrisonCommonsLog.Decision(pawn, "VACATE",
			$"enqueue {PrisonCommonsLog.JobLabel(job)} | {PrisonCommonsLog.HangoutSnapshot(pawn)}");
		pawn.jobs.jobQueue.EnqueueFirst(job);
	}

	/// <summary>
	/// Doorway is allowed when the connected doorway graph (plus at most one enclosed
	/// non-doorway hop) touches two different matching rooms: the prisoner's home
	/// (owned bedroom, or current prison cell if bedless) and/or any access commons.
	/// </summary>
	public static bool IsAllowedDoorway(Pawn? p, Room? room)
	{
		if (p == null || room is not { IsDoorway: true })
			return false;

		var prisonerHome = GetPrisonerHomeRoom(p);

		Room? firstMatchingRoom = null;
		var seenDoors = new HashSet<Room> { room };
		var doorQueue = new Queue<(Room door, bool usedHallHop)>();
		doorQueue.Enqueue((room, false));

		while (doorQueue.Count != 0)
		{
			var (door, usedHallHop) = doorQueue.Dequeue();
			var regions = door.Regions;
			if (regions == null)
				continue;

			for (var i = 0; i < regions.Count; i++)
			{
				var region = regions[i];
				if (region?.Neighbors == null)
					continue;

				foreach (var neighbor in region.Neighbors)
				{
					var neighborRoom = neighbor?.Room;
					if (neighborRoom == null)
						continue;

					if (IsMatchingAccessRoom(neighborRoom, prisonerHome))
					{
						if (firstMatchingRoom is null)
							firstMatchingRoom = neighborRoom;
						else if (firstMatchingRoom != neighborRoom)
							return true;
					}
					else if (neighborRoom.IsDoorway && seenDoors.Add(neighborRoom))
					{
						doorQueue.Enqueue((neighborRoom, usedHallHop));
					}
					else if (!usedHallHop && !neighborRoom.IsDoorway && !neighborRoom.TouchesMapEdge)
					{
						ConsiderHallHop(neighborRoom, prisonerHome, seenDoors, doorQueue, ref firstMatchingRoom,
							out var found);
						if (found)
							return true;
					}
				}
			}
		}

		return false;
	}

	private static Room? GetPrisonerHomeRoom(Pawn p)
	{
		var bedRoom = p.ownership?.OwnedBed?.GetRoom();
		if (bedRoom != null)
			return bedRoom;

		var current = p.GetRoom();
		if (current is { IsPrisonCell: true })
			return current;

		return null;
	}

	private static bool IsMatchingAccessRoom(Room room, Room? prisonerHome)
		=> room == prisonerHome || IsAccessCommons(room);

	private static bool DoorwayTouchesLeaveCommons(Room doorway)
	{
		var regions = doorway.Regions;
		if (regions == null)
			return false;

		for (var i = 0; i < regions.Count; i++)
		{
			var region = regions[i];
			if (region?.Neighbors == null)
				continue;

			foreach (var neighbor in region.Neighbors)
			{
				if (IsLeaveCommons(neighbor?.Room))
					return true;
			}
		}

		return false;
	}

	private static void ConsiderHallHop(
		Room hall,
		Room? prisonerHome,
		HashSet<Room> seenDoors,
		Queue<(Room door, bool usedHallHop)> doorQueue,
		ref Room? firstMatchingRoom,
		out bool foundSecondMatch)
	{
		foundSecondMatch = false;
		var hallRegions = hall.Regions;
		if (hallRegions == null)
			return;

		for (var h = 0; h < hallRegions.Count; h++)
		{
			var hallRegion = hallRegions[h];
			if (hallRegion?.Neighbors == null)
				continue;

			foreach (var hallNeighbor in hallRegion.Neighbors)
			{
				var beyond = hallNeighbor?.Room;
				if (beyond == null)
					continue;

				if (IsMatchingAccessRoom(beyond, prisonerHome))
				{
					if (firstMatchingRoom is null)
						firstMatchingRoom = beyond;
					else if (firstMatchingRoom != beyond)
					{
						foundSecondMatch = true;
						return;
					}
				}
				else if (beyond.IsDoorway && seenDoors.Add(beyond))
				{
					doorQueue.Enqueue((beyond, true));
				}
			}
		}
	}

	/// <summary>
	/// Prisoners may stand in their current room, owned bedroom, or linger commons
	/// (vanilla HostFaction chew-spot search only allows the current room).
	/// </summary>
	public static bool IsAllowedPrisonerStandRoom(Pawn pawn, Room? room)
	{
		if (pawn == null || room == null)
			return false;
		if (room == pawn.GetRoom())
			return true;
		if (IsLingerCommons(room))
			return true;
		var home = GetPrisonerHomeRoom(pawn);
		return home != null && room == home;
	}

	public static bool ShouldUseCommonsChewSpot(Pawn? pawn, Thing? ingestible)
	{
		if (pawn is not { IsPrisonerOfColony: true, Spawned: true } || ingestible is not { Spawned: true })
			return false;
		if (ingestible.Map != pawn.Map)
			return false;

		Room? foodRoom = GetIngestibleRoom(ingestible);
		if (foodRoom == null || !IsLingerCommons(foodRoom))
			return false;

		// Already in that room — vanilla same-room search is fine.
		return foodRoom != pawn.GetRoom();
	}

	public static bool TryFindCommonsFoodFor(Pawn pawn, out Thing food, out ThingDef foodDef)
	{
		food = null!;
		foodDef = null!;
		if (pawn?.Map == null || !pawn.Spawned)
			return false;

		bool desperate = pawn.needs.food.CurCategory == HungerCategory.Starving;
		Thing? best = null;
		ThingDef? bestDef = null;
		float bestOpt = float.MinValue;
		var seenRooms = new HashSet<Room>();

		foreach (var comp in comps)
		{
			if (!comp.parent.Spawned || comp.parent.Map != pawn.Map)
				continue;
			if (!IsLingerMarker(comp))
				continue;

			Room? room = comp.parent.GetRoom();
			if (room == null || !IsLingerCommons(room) || !seenRooms.Add(room))
				continue;

			var regions = room.Regions;
			if (regions == null)
				continue;

			for (int r = 0; r < regions.Count; r++)
			{
				Region? region = regions[r];
				if (region == null)
					continue;

				List<Thing> things = region.ListerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree);
				for (int i = 0; i < things.Count; i++)
				{
					Thing t = things[i];
					if (!IsUsableCommonsFood(pawn, t, desperate, out ThingDef def))
						continue;

					float dist = (pawn.Position - t.Position).LengthManhattan;
					float opt = FoodUtility.FoodOptimality(pawn, t, def, dist);
					if (opt <= bestOpt)
						continue;

					best = t;
					bestDef = def;
					bestOpt = opt;
				}
			}
		}

		if (best == null || bestDef == null)
			return false;

		food = best;
		foodDef = bestDef;
		return true;
	}

	public static bool TryFindChewSpotNearFood(Pawn pawn, Thing ingestible, out IntVec3 cell)
	{
		cell = IntVec3.Invalid;
		if (pawn?.Map == null || ingestible == null)
			return false;

		Map map = pawn.Map;
		IntVec3 root = ingestible is Building_NutrientPasteDispenser npd
			? npd.InteractionCell
			: ingestible.Position;
		float radius = ingestible.def.ingestible?.chairSearchRadius ?? 8f;
		if (radius < 4f)
			radius = 4f;

		Thing? chair = GenClosest.ClosestThingReachable(
			root,
			map,
			ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial),
			PathEndMode.OnCell,
			TraverseParms.For(pawn),
			radius,
			t => IsValidCommonsChair(t, pawn) && t.Position.GetDangerFor(pawn, map) == Danger.None);

		if (chair != null && Toils_Ingest.TryFindFreeSittingSpotOnThing(chair, pawn, out cell) && cell.IsValid)
			return true;

		int maxCells = GenRadial.NumCellsInRadius(Math.Min(radius, 12f));
		for (int i = 0; i < maxCells; i++)
		{
			IntVec3 c = root + GenRadial.RadialPattern[i];
			if (!c.InBounds(map) || !c.Standable(map))
				continue;
			if (!IsAllowedPrisonerStandRoom(pawn, c.GetRoom(map)))
				continue;
			if (!pawn.CanReserveSittableOrSpot(c))
				continue;
			if (!pawn.CanReach(c, PathEndMode.OnCell, Danger.Some))
				continue;
			if (!Toils_Ingest.TryFindAdjacentIngestionPlaceSpot(c, GetIngestibleDef(ingestible), pawn, out _))
				continue;

			cell = c;
			return true;
		}

		return false;
	}

	private static Room? GetIngestibleRoom(Thing ingestible)
	{
		IntVec3 spot = ingestible.def.hasInteractionCell ? ingestible.InteractionCell : ingestible.Position;
		return spot.GetRoom(ingestible.Map);
	}

	private static ThingDef GetIngestibleDef(Thing ingestible)
	{
		if (ingestible is Building_NutrientPasteDispenser npd && npd.DispensableDef != null)
			return npd.DispensableDef;
		return FoodUtility.GetFinalIngestibleDef(ingestible);
	}

	private static bool IsUsableCommonsFood(Pawn pawn, Thing t, bool desperate, out ThingDef def)
	{
		def = null!;
		if (t == null || !t.Spawned)
			return false;

		if (t is Building_NutrientPasteDispenser npd)
		{
			if (!npd.CanDispenseNow)
				return false;
			if (!pawn.CanReach(npd.InteractionCell, PathEndMode.OnCell, Danger.Some))
				return false;
			def = ThingDefOf.MealNutrientPaste;
			return pawn.WillEat(def, pawn);
		}

		if (!t.def.IsNutritionGivingIngestible || !t.IngestibleNow)
			return false;
		if (t.IsForbidden(pawn) || t.IsDessicated())
			return false;
		if (!desperate && t.IsNotFresh())
			return false;
		if (!pawn.WillEat(t, pawn))
			return false;
		if (!pawn.CanReserve(t, 10, 1))
			return false;
		if (!pawn.CanReach(t, PathEndMode.ClosestTouch, Danger.Some))
			return false;

		def = FoodUtility.GetFinalIngestibleDef(t);
		return def != null;
	}

	private static bool IsValidCommonsChair(Thing t, Pawn pawn)
	{
		if (t.def.building is not { isSittable: true })
			return false;
		if (t.IsForbidden(pawn) || t.IsBurning() || t.HostileTo(pawn))
			return false;
		if (!pawn.CanReserve(t))
			return false;
		if (!Toils_Ingest.TryFindFreeSittingSpotOnThing(t, pawn, out IntVec3 sit))
			return false;
		if (!IsAllowedPrisonerStandRoom(pawn, sit.GetRoom(t.Map)))
			return false;

		for (int i = 0; i < 4; i++)
		{
			Building? edifice = (sit + GenAdj.CardinalDirections[i]).GetEdifice(t.Map);
			if (edifice is { def.surfaceType: SurfaceType.Eat })
				return true;
		}

		return false;
	}
}
