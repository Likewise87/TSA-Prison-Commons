using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonCommons;

/// <summary>
/// Vanilla resistance mood curve tops out at 1.5× at 100% mood.
/// Raise the top to 1.8× (still lerped from 1.0× at 50%).
/// </summary>
[StaticConstructorOnStartup]
internal static class RecruitResistanceMoodCurve_Patch
{
	static RecruitResistanceMoodCurve_Patch()
	{
		try
		{
			FieldInfo? field = AccessTools.Field(typeof(InteractionWorker_RecruitAttempt), "ResistanceImpactFactorCurve_Mood");
			if (field == null)
			{
				Log.Warning("[TSA Prison Commons] ResistanceImpactFactorCurve_Mood field not found.");
				return;
			}

			var curve = new SimpleCurve
			{
				new CurvePoint(0f, 0.2f),
				new CurvePoint(0.5f, 1f),
				new CurvePoint(1f, 1.8f)
			};
			field.SetValue(null, curve);
			Log.Message("[TSA Prison Commons] Recruit resistance mood curve top set to 1.8× at 100% mood.");
		}
		catch (System.Exception e)
		{
			Log.Error($"[TSA Prison Commons] Failed to patch recruit mood curve: {e}");
		}
	}
}
