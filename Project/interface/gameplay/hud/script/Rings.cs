using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class Rings : Control
{
	private StageSettings Stage => StageSettings.Instance;
	[ExportGroup("Rings")]
	[Export]
	private Label ringLabel;
	[Export]
	private Label maxRingLabel;
	[Export]
	private Label ringLossLabel;
	[Export]
	private Sprite2D ringDividerSprite;
	[Export]
	private AnimationTree ringAnimator;
	[Export]
	private AnimationPlayer fireSoulAnimator;
	[Export]
	private bool enableHudDecal;
	[Export]
	private Sprite2D hudDecal;
	[Export]
	private Sprite2D hudDecalShort;

	private const string RingLabelFormat = "000";
	private readonly string EnabledParameter = "enabled";
	private readonly string DisabledParameter = "disabled";

	private readonly string RingGainParameter = "parameters/gain_trigger/request";
	private readonly string RingLossParameter = "parameters/loss_trigger/request";
	private readonly string RinglessParameter = "parameters/ringless_transition/transition_request";

	public void InitializeRings()
	{
		// Initialize ring counter
		if (Stage != null)
		{

			if (hudDecal != null && hudDecalShort != null)
			{
				GD.Print("Ring hud decale is true");
				if (Stage.Data.MissionType == LevelDataResource.MissionTypes.Ring && Stage.Data.MissionObjectiveCount != 0)
				{
					GD.Print("Setting ring mission decal visibility");
					hudDecal.Visible = true;
					hudDecalShort.Visible = false;
				}
				else
				{
					GD.Print("Setting ring decal visibility");
					hudDecal.Visible = false;
					hudDecalShort.Visible = true;
				}

			}

			maxRingLabel.Visible = ringDividerSprite.Visible = Stage.Data.MissionType == LevelDataResource.MissionTypes.Ring &&
				Stage.Data.MissionObjectiveCount != 0; // Show/Hide max ring count
			if (maxRingLabel.Visible)
				maxRingLabel.Text = Stage.Data.MissionObjectiveCount.ToString(RingLabelFormat);

			ringAnimator.Active = true;
			UpdateRingCount(Stage.CurrentRingCount, true);

		}
	}

	public void UpdateRingCount(int amount, bool disableAnimations)
	{
		if (!disableAnimations) // Play animation
		{
			if (amount >= 0)
			{
				ringAnimator.Set(RingGainParameter, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			}
			else
			{
				ringLossLabel.Text = amount.ToString();
				ringAnimator.Set(RingLossParameter, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			}
		}

		ringAnimator.Set(RinglessParameter, Stage.CurrentRingCount == 0 ? EnabledParameter : DisabledParameter);
		ringLabel.Text = Stage.CurrentRingCount.ToString(RingLabelFormat);
	}
}
