using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class Rings : Control
{
	[Signal] private delegate void RingChainFinishedEventHandler();

	private StageSettings Stage => StageSettings.Instance;

	[Export] private AnimationPlayer styleAnimator;
	[Export] private Label ringLabel;
	[Export] private Label maxRingLabel;
	[Export] private Label ringLossLabel;
	[Export] private AnimationTree ringAnimator;

	private const string RingLabelFormat = "000";
	private readonly string EnabledParameter = "enabled";
	private readonly string DisabledParameter = "disabled";

	private readonly string RingGainParameter = "parameters/gain_trigger/request";
	private readonly string RingLossParameter = "parameters/loss_trigger/request";
	private readonly string RinglessParameter = "parameters/ringless_transition/transition_request";
	private readonly string RingGrowParameter = "parameters/grow_blend/add_amount";

	public void InitializeRings()
	{
		// Initialize ring counter

		switch (SaveManager.Config.hudStyle)
		{
			case SaveManager.HudStyle.Reignition:
				styleAnimator.Play("reignited");
				styleAnimator.Advance(0.0);
				break;
		}

		// Show/Hide max ring count
		bool isRingLimited = Stage.Data.MissionType == LevelDataResource.MissionTypes.Ring && Stage.Data.MissionObjectiveCount != 0;
		styleAnimator.Play(isRingLimited ? "ring-limit" : "ring-no-limit");

		if (isRingLimited)
			maxRingLabel.Text = Stage.Data.MissionObjectiveCount.ToString(RingLabelFormat);

		ringAnimator.Active = true;
		UpdateRingCount(Stage.CurrentRingCount, true);
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
