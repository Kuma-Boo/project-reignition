using Godot;

namespace Project.Gameplay.Triggers;

/// <summary>
/// Force the player to jump to the StageTriggerModule's position.
/// </summary>
[Tool]
public partial class JumpTrigger : StageTriggerModule
{
	[Signal]
	/// <summary> Called when jump is finished. </summary>
	public delegate void JumpFinishedEventHandler();

	/// <summary> How high to jump. </summary>
	[Export] public float jumpHeight;
	/// <summary> Auto align jump direction? </summary>
	[Export] public bool autoAlign;
	/// <summary> Should height be relative to end? </summary>
	[Export] public bool forceConsistentHeight;

	public LaunchSettings GetLaunchSettings()
	{
		Vector3 startPosition = Player != null ? Player.GlobalPosition : GetParent<Node3D>().GlobalPosition;
		LaunchSettings settings = LaunchSettings.Create(startPosition, GlobalPosition, jumpHeight, forceConsistentHeight);
		settings.IsJump = true;
		settings.UseAutoAlign = autoAlign;
		settings.AllowJumpDash = false;
		return settings;
	}

	private void FinishJump()
	{
		Player.LaunchFinished -= FinishJump;
		EmitSignal(SignalName.JumpFinished);
	}

	public override void Activate()
	{
		Player.StartLauncher(GetLaunchSettings());
		Player.Effect.StopSpinFX();
		Player.Effect.StopTrailFX();
		Player.LaunchFinished += FinishJump;
	}
}