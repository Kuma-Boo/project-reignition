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
		Vector3 startPosition = Player != null ? Player.GlobalPosition : GetParent<Node3D>().GlobalPosition;
		GD.Print(GlobalPosition);
		LaunchSettings settings = LaunchSettings.Create(startPosition, GlobalPosition, jumpHeight, forceConsistentHeight);
		settings.IsJump = true;
		settings.UseAutoAlign = autoAlign;
		settings.AllowJumpDash = false;
		return settings;
	}

	private void FinishJump() => EmitSignal(SignalName.JumpFinished);

	public override void Activate()
	{
		Player.StartLauncher(GetLaunchSettings());

		if (!Player.IsConnected(PlayerController.SignalName.LaunchFinished, new Callable(this, MethodName.FinishJump)))
			Player.Connect(PlayerController.SignalName.LaunchFinished, new Callable(this, MethodName.FinishJump), (uint)ConnectFlags.OneShot);
	}
}