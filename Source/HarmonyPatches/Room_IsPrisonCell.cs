using HarmonyLib;
using Verse;

namespace PrisonCommons;

[HarmonyPatch(typeof(Room), nameof(Room.IsPrisonCell), MethodType.Getter)]
internal static class Room_IsPrisonCell
{
	static readonly AccessTools.FieldRef<Room, bool>? IsPrisonCellField =
		AccessTools.FieldRefAccess<Room, bool>("isPrisonCell");

	public static void Postfix(ref bool __result, Room __instance)
	{
		if (__result || __instance == null)
			return;

		if (!PrisonCommons.IsLingerCommons(__instance))
			return;

		__result = true;
		// Keep vanilla's cached field in sync (DrawFieldEdges and similar read the field).
		if (IsPrisonCellField != null)
			IsPrisonCellField(__instance) = true;
	}
}
