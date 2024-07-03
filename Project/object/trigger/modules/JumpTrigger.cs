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

	[Export]
	/// <summary> How high to jump. </summary>
	public float jumpHeight;
	[Export]
	/// <summary> Auto align jump direction? </summary>
	public bool autoAlign;
	[Export]
	/// <summary> Should height be relative to end? </summary>
	public bool forceConsistentHeight;

	public LaunchSettings GetLaunchSettings()
	{
		Vector3 startPosition = Character != null ? Character.GlobalPosition : GetParent<Node3D>().GlobalPosition;
		LaunchSettings settings = LaunchSettings.Create(startPosition, GlobalPosition, jumpHeight, forceConsistentHeight);
		settings.IsJump = true;
		settings.UseAutoAlign = autoAlign;
		return settings;
	}

	private void FinishJump() => EmitSignal(SignalName.JumpFinished);

	public override void Activate()
	{
		Character.StartLauncher(GetLaunchSettings());
		Character.Connect(CharacterController.SignalName.LaunchFinished, new Callable(this, MethodName.FinishJump), (uint)ConnectFlags.OneShot);
		Character.CanJumpDash = false;
	}
}