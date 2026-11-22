using HarmonyLib;
using Verse;

namespace PrisonCommons;

/// <summary>
/// Vanilla caches prison status in a private <c>isPrisonCell</c> field (beds only).
/// The getter postfix makes <see cref="Room.IsPrisonCell"/> true for commons, but code that
/// reads the field (e.g. <c>DrawFieldEdges</c>) still sees false. Keep the field in sync.
/// Does not force beds to ForPrisoners — commons may be recreation/dining without prisoner beds.
/// </summary>
[HarmonyPatch(typeof(Room), nameof(Room.Notify_RoomShapeChanged))]
internal static class Room_Notify_RoomShapeChanged
{
	static readonly AccessTools.FieldRef<Room, bool>? IsPrisonCellField =
		AccessTools.FieldRefAccess<Room, bool>("isPrisonCell");

	public static void Postfix(Room __instance)
	{
		if (__instance == null || IsPrisonCellField == null)
			return;

		if (IsPrisonCellField(__instance))
			return;

		if (PrisonCommons.IsLingerCommons(__instance))
			IsPrisonCellField(__instance) = true;
	}
}
