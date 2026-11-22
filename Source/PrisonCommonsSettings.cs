using Verse;

namespace PrisonCommons;

public class PrisonCommonsSettings : ModSettings
{
	public bool enableLogging;

	public override void ExposeData()
	{
		Scribe_Values.Look(ref enableLogging, "enableLogging", false);
	}
}
