using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PrisonCommons;

/// <summary>
/// Shows private-cell prison-break status on the prisoner pawn inspect pane.
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
internal static class Pawn_GetInspectString_PrivateCell
{
	static void Postfix(Pawn __instance, ref string __result)
	{
		if (__instance == null || !__instance.IsPrisonerOfColony)
			return;

		string line = PrisonBreakUtility_InitiatePrisonBreakMtbDays.GetInspectStatusLine(__instance);
		if (line.NullOrEmpty())
			return;

		var sb = new StringBuilder(__result ?? string.Empty);
		if (sb.Length > 0)
			sb.AppendLine();
		sb.Append(line);
		__result = sb.ToString();
	}
}
