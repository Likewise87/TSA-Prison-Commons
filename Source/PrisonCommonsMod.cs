using UnityEngine;
using Verse;

namespace PrisonCommons;

public class PrisonCommonsMod : Mod
{
	public static PrisonCommonsSettings Settings = null!;

	public PrisonCommonsMod(ModContentPack content) : base(content)
	{
		Settings = GetSettings<PrisonCommonsSettings>();
	}

	public override string SettingsCategory()
		=> "TSA Prison Commons";

	public override void DoSettingsWindowContents(Rect inRect)
	{
		var listing = new Listing_Standard();
		listing.Begin(inRect);
		listing.CheckboxLabeled(
			"PrisonCommons_Settings_EnableLogging".Translate(),
			ref Settings.enableLogging,
			"PrisonCommons_Settings_EnableLoggingTip".Translate());
		listing.End();
		base.DoSettingsWindowContents(inRect);
	}
}
