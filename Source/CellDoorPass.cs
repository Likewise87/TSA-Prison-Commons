using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PrisonCommons;

/// <summary>
/// ReBuild / VFE cell doors are the supported separators between private cells and commons.
/// Colony prisoners may always pass them (no doorway-BFS required).
/// </summary>
internal static class CellDoorPass
{
	private static readonly HashSet<string> CellDoorDefNames = new()
	{
		"RB_CellDoor",
		"VFEArch_CellDoor"
	};

	public static bool IsPrisonCellDoor(Building_Door? door)
	{
		if (door?.def == null)
			return false;

		if (CellDoorDefNames.Contains(door.def.defName))
			return true;

		// ReBuild marks lock doors with prisonersCannotOpenIt.
		return HasPrisonersCannotOpenExtension(door.def);
	}

	public static bool CanColonyPrisonerPass(Pawn? p, Building_Door door)
	{
		if (p == null || !IsPrisonCellDoor(door))
			return false;

		if (!p.IsPrisoner)
			return false;

		// Colony prisoners / host-faction guests — not wild hostile inmates behind enemy doors.
		if (p.IsPrisonerOfColony)
			return true;

		return p.guest?.HostFaction != null && p.guest.HostFaction == door.Faction;
	}

	private static bool HasPrisonersCannotOpenExtension(ThingDef def)
	{
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

			var field = AccessTools.Field(ext.GetType(), "prisonersCannotOpenIt");
			if (field != null && field.GetValue(ext) is true)
				return true;
		}

		return false;
	}
}
