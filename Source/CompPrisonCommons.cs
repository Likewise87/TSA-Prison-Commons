using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimWorld;

[StaticConstructorOnStartup]
public class CompPrisonCommons : ThingComp
{
	private static readonly Texture2D forPrisonersTex = ContentFinder<Texture2D>.Get("UI/Commands/ForPrisoners");

	private bool isPrisonCommons;

	private bool Active
	{
		get => isPrisonCommons;
		set
		{
			if (isPrisonCommons == value)
				return;

			isPrisonCommons = value;
			if (parent.Spawned)
				PrisonCommons.PrisonCommons.SetPrisonCommons(this, isPrisonCommons);
		}
	}

	private bool IsAccessOnlyMarker
		=> PrisonCommons.PrisonCommons.IsHygieneAccessMarker(this);

	public override void PostExposeData()
	{
		Scribe_Values.Look(ref isPrisonCommons, "isPrisonCommons");
	}

	public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
	{
		base.PostDeSpawn(map, mode);
		if (Active)
			PrisonCommons.PrisonCommons.SetPrisonCommons(this, false);
	}

	public override void PostDestroy(DestroyMode mode, Map previousMap)
	{
		base.PostDestroy(mode, previousMap);
		if (Active)
			PrisonCommons.PrisonCommons.SetPrisonCommons(this, false);
	}

	public override void PostSpawnSetup(bool respawningAfterLoad)
	{
		base.PostSpawnSetup(respawningAfterLoad);
		if (parent.Faction != Faction.OfPlayer && !respawningAfterLoad)
			isPrisonCommons = false;

		if (Active)
			PrisonCommons.PrisonCommons.SetPrisonCommons(this, true);
	}

	public override IEnumerable<Gizmo> CompGetGizmosExtra()
	{
		bool joyBuilding = PrisonCommons.PrisonCommons.IsJoyBuilding(parent.def);

		string label;
		string activeDesc;
		string inactiveDesc;
		// Joy first — do not classify recreation as hygiene "access only".
		if (joyBuilding)
		{
			label = "CommandPrisonCommonsToggleLabelJoy".Translate();
			activeDesc = "CommandPrisonCommonsToggleDescActiveJoy".Translate();
			inactiveDesc = "CommandPrisonCommonsToggleDescInactiveJoy".Translate();
		}
		else if (IsAccessOnlyMarker)
		{
			label = "CommandPrisonCommonsToggleLabelAccessOnly".Translate();
			activeDesc = "CommandPrisonCommonsToggleDescActiveAccessOnly".Translate();
			inactiveDesc = "CommandPrisonCommonsToggleDescInactiveAccessOnly".Translate();
		}
		else
		{
			label = "CommandPrisonCommonsToggleLabel".Translate();
			activeDesc = "CommandPrisonCommonsToggleDescActive".Translate();
			inactiveDesc = "CommandPrisonCommonsToggleDescInactive".Translate();
		}

		var commandToggle = new Command_Toggle
		{
			defaultLabel = label,
			icon = forPrisonersTex,
			isActive = () => Active,
			toggleAction = delegate { Active = !Active; },
			defaultDesc = Active ? activeDesc : inactiveDesc
		};

		yield return commandToggle;
	}
}
