using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PrisonCommons;

/// <summary>
/// DBH disables OccupancyCheck when ForPrisoners, and allows any prisoner any fixture.
/// Honor OccupancyCheckInt always, and limit prisoners to access-commons / prison-cell fixtures.
/// </summary>
internal static class DbhOccupancyAndPrisonerFixturePatch
{
	private static Type? FixtureType;
	private static Type? ClosestSanitationType;
	private static AccessTools.FieldRef<object, bool>? OccupancyCheckIntField;
	private static AccessTools.FieldRef<object, bool>? AnimalsOnlyField;

	private static bool ResolveTypes()
	{
		FixtureType ??= AccessTools.TypeByName("DubsBadHygiene.Building_AssignableFixture");
		ClosestSanitationType ??= AccessTools.TypeByName("DubsBadHygiene.ClosestSanitation");
		if (FixtureType == null || ClosestSanitationType == null)
			return false;

		OccupancyCheckIntField ??= AccessTools.FieldRefAccess<object, bool>(
			AccessTools.Field(FixtureType, "OccupancyCheckInt")
			?? throw new InvalidOperationException("OccupancyCheckInt missing"));
		AnimalsOnlyField ??= AccessTools.FieldRefAccess<object, bool>(
			AccessTools.Field(FixtureType, "AnimalsOnly")
			?? throw new InvalidOperationException("AnimalsOnly missing"));
		return true;
	}

	private static bool WantsOccupancyCheck(object fixture)
	{
		if (OccupancyCheckIntField == null || AnimalsOnlyField == null)
			return false;
		return OccupancyCheckIntField(fixture) && !AnimalsOnlyField(fixture);
	}

	private static bool RoomLooksOccupied(Thing fixture, Pawn checker)
	{
		var room = fixture.GetRoom();
		if (room == null)
			return false;

		var map = fixture.Map;
		foreach (var thing in room.ContainedAndAdjacentThings)
		{
			if (thing is Pawn other && other != checker)
				return true;
			if (map.reservationManager.IsReservedByAnyoneOf(thing, checker.Faction))
				return true;
		}

		return false;
	}

	/// <summary>Always honor OccupancyCheckInt (DBH zeroes OccupancyCheck when ForPrisoners).</summary>
	[HarmonyPatch]
	internal static class Occupied_Patch
	{
		static bool Prepare() => ResolveTypes();

		static MethodBase TargetMethod()
			=> AccessTools.Method(ClosestSanitationType, "Occupied")
			   ?? throw new InvalidOperationException("ClosestSanitation.Occupied not found");

		static bool Prefix(object fixture, Pawn checker, ref bool __result)
		{
			if (fixture is not Thing fixtureThing || checker == null)
				return true;

			if (!WantsOccupancyCheck(fixture))
			{
				__result = false;
				return false;
			}

			__result = RoomLooksOccupied(fixtureThing, checker);
			return false;
		}
	}

	/// <summary>Prisoners may only use fixtures in prison cells or Prison Commons access rooms.</summary>
	[HarmonyPatch]
	internal static class IsEverUsable_Patch
	{
		static bool Prepare() => ResolveTypes();

		static MethodBase TargetMethod()
			=> AccessTools.Method(ClosestSanitationType, "IsEverUsable")
			   ?? throw new InvalidOperationException("ClosestSanitation.IsEverUsable not found");

		static void Postfix(Thing x, Pawn user, ref bool __result)
		{
			if (!__result || user is not { IsPrisonerOfColony: true } || x == null)
				return;

			if (!FixtureType!.IsInstanceOfType(x))
				return;

			var room = x.GetRoom();
			if (PrisonCommons.IsAccessCommons(room))
				return;

			if (x.Position.IsInPrisonCell(x.Map))
				return;

			__result = false;
		}
	}

	/// <summary>Occupancy blocks even when the need is urgent.</summary>
	[HarmonyPatch]
	internal static class UsableNow_Patch
	{
		static bool Prepare() => ResolveTypes();

		static MethodBase TargetMethod()
			=> AccessTools.Method(ClosestSanitationType, "UsableNow")
			   ?? throw new InvalidOperationException("ClosestSanitation.UsableNow not found");

		static void Postfix(Thing x, Pawn pawn, ref bool __result)
		{
			if (!__result || pawn == null || x == null)
				return;

			if (!FixtureType!.IsInstanceOfType(x))
				return;

			if (!WantsOccupancyCheck(x))
				return;

			if (RoomLooksOccupied(x, pawn))
				__result = false;
		}
	}
}
