using HarmonyLib;
using RimWorld;
using Verse;

namespace PrisonCommons;

/// <summary>
/// Let private prison cells use the better of their own Beauty/Space/Wealth/Cleanliness
/// and an adjacent linger commons — feeds prison-cell impressiveness without merging rooms.
/// </summary>
[HarmonyPatch(typeof(Room), nameof(Room.GetStat))]
internal static class Room_GetStat
{
	public static void Postfix(Room __instance, RoomStatDef roomStat, ref float __result)
	{
		if (roomStat != RoomStatDefOf.Beauty
		    && roomStat != RoomStatDefOf.Space
		    && roomStat != RoomStatDefOf.Wealth
		    && roomStat != RoomStatDefOf.Cleanliness)
			return;
		if (!PrivateCellRoomStats.ShouldBorrow(__instance))
			return;
		if (!PrivateCellRoomStats.TryGetLinkedLingerCommons(__instance, out Room commons))
			return;

		// Commons GetStat is safe: linger rooms are excluded from ShouldBorrow (no recursion).
		float commonsStat = commons.GetStat(roomStat);
		__result = PrivateCellRoomStats.Borrow(__result, commonsStat);
	}
}
