using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PrisonCommons;

[HarmonyPatch(typeof(WorkGiver_Warden_DeliverFood), "FoodAvailableInRoomTo")]
internal static class WorkGiver_Warden_DeliverFood_FoodAvailableInRoomTo
{
	public static bool Postfix(bool __result, Pawn prisoner)
	{
		if (__result)
			return true;

		var room = prisoner.GetRoom();
		if (room == null)
			return false;

		var seenRooms = new HashSet<Room>();
		float neededNutrition = 0f, availableNutrition = 0f;
		CountNutrition(seenRooms, prisoner, room, ref neededNutrition, ref availableNutrition);

		return availableNutrition + 0.5f >= neededNutrition;
	}

	[HarmonyReversePatch]
	[HarmonyPatch(typeof(WorkGiver_Warden_DeliverFood), "NutritionAvailableForFrom")]
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static float NutritionAvailableForFrom(Pawn p, Thing foodSource)
	{
		throw new NotImplementedException("stub method should not be callable");
	}

	private static void CountNutrition(HashSet<Room> seenRooms, Pawn prisoner, Room room, ref float neededNutrition,
		ref float availableNutrition)
	{
		if (!seenRooms.Add(room))
			return;

		// Snapshot live lists — recursion through commons doorways can rebuild Regions mid-foreach.
		var regions = new List<Region>(room.Regions);
		foreach (var region in regions)
		{
			var things = region.ListerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree);
			for (int i = 0; i < things.Count; i++)
			{
				var thing = things[i];
				if ((!thing.def.IsIngestible ||
				     thing.def.ingestible.preferability > FoodPreferability.DesperateOnlyForHumanlikes) &&
				    prisoner.CanReach(thing, PathEndMode.ClosestTouch, Danger.Some))
				{
					availableNutrition += NutritionAvailableForFrom(prisoner, thing);
				}
			}

			var pawns = region.ListerThings.ThingsInGroup(ThingRequestGroup.Pawn);
			for (int i = 0; i < pawns.Count; i++)
			{
				var thing = pawns[i];
				if (thing is Pawn { IsPrisonerOfColony: true } pawn && pawn.needs.food != null &&
				    pawn.needs.food.CurLevelPercentage < pawn.needs.food.PercentageThreshHungry + 0.02f &&
				    (pawn.carryTracker.CarriedThing == null || !pawn.WillEat(pawn.carryTracker.CarriedThing)))
				{
					neededNutrition += pawn.needs.food.NutritionWanted;
				}
			}

			var links = new List<RegionLink>(region.links);
			foreach (var link in links)
			{
				var other = link.GetOtherRegion(region);
				if (other.type != RegionType.Portal || !seenRooms.Add(other.Room) ||
				    !PrisonCommons.IsAllowedDoorway(prisoner, other.Room))
				{
					continue;
				}

				var neighbors = new List<Region>(other.Neighbors);
				foreach (var neighbor in neighbors)
					CountNutrition(seenRooms, prisoner, neighbor.Room, ref neededNutrition, ref availableNutrition);
			}
		}
	}
}
