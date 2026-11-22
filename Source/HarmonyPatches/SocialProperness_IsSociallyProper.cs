using HarmonyLib;
using RimWorld;
using Verse;

namespace PrisonCommons;

[HarmonyPatch(typeof(SocialProperness), nameof(SocialProperness.IsSociallyProper), typeof(Thing), typeof(Pawn),
	typeof(bool), typeof(bool))]
internal static class SocialProperness_IsSociallyProper
{
	public static void Postfix(ref bool __result, Thing t, Pawn p, bool forPrisoner)
	{
		if (p == null || t?.Map == null)
			return;

		var interactionSpot = t.def.hasInteractionCell ? t.InteractionCell : t.Position;
		var interactionRoom = interactionSpot.GetRoomOrAdjacent(t.Map);

		// Colonists: never use prisoner-marked joy buildings or linger-commons recreation.
		if (!forPrisoner)
		{
			if (PrisonCommons.IsPrisonerOnlyJoyBuilding(t))
			{
				__result = false;
				return;
			}

			if (interactionRoom != null && PrisonCommons.IsLingerCommons(interactionRoom) &&
			    PrisonCommons.IsJoyBuilding(t.def))
			{
				__result = false;
				return;
			}

			return;
		}

		// --- Prisoner path ---
		if (interactionRoom == null)
			return;

		// Leave bathrooms: hygiene fixtures only (override vanilla same-room hangout).
		if (PrisonCommons.IsLeaveCommons(interactionRoom))
		{
			__result = PrisonCommons.IsHygieneThing(t);
			return;
		}

		if (__result)
			return;

		// Explicit For-prisoners joy building (chess etc.).
		if (PrisonCommons.IsPrisonerOnlyJoyBuilding(t))
		{
			__result = true;
			return;
		}

		// Linger commons / allowed doorways stay socially proper for prisoners.
		if (PrisonCommons.IsLingerCommons(interactionRoom) ||
		    PrisonCommons.IsAllowedDoorway(p, interactionRoom))
		{
			__result = true;
			return;
		}

		// Bedroom always proper (needed for FindBedFor / downed rescue from leave rooms).
		var prisonerBedroom = p.ownership?.OwnedBed?.GetRoom();
		if (interactionRoom == prisonerBedroom)
			__result = true;
	}
}
