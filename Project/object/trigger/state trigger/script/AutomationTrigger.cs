using Godot;

namespace Project.Gameplay.Triggers;

/// <summary>
/// Force the player to move along a path.
/// </summary>
public partial class AutomationTrigger : Area3D
{
	[Signal]
	public delegate void ActivatedEventHandler();
	[Signal]
	public delegate void DeactivatedEventHandler();

	/// <summary> The distance along the path where automation stops. </summary>
	[Export] private float endPoint;
	/// <summary> Always activate regardless of which way the player entered/moves. </summary>
	[Export] private bool ignoreDirection;
	/// <summary> Queue the automation to start after landing even if the player is in the air. </summary>
	[Export] private bool autoQueueOnLand = true;
	/// <summary> Calculate activation using a specific path? </summary>
	[Export] private Path3D path;
	private bool isInteractingWithPlayer;
	private bool isAutomationQueued;
	private PlayerController Player => StageSettings.Player;
	public bool IsFinished => Player.PathFollower.Progress >= endPoint;

	public override void _Ready() => StageSettings.Instance.Respawned += Respawn;

	public override void _PhysicsProcess(double _)
	{
		if (!isInteractingWithPlayer && !isAutomationQueued)
			return;

		AttemptAutomation();
	}

	private void Respawn() => isAutomationQueued = false;

	private void AttemptAutomation()
	{
		if (!Player.IsOnGround)
			return;

		if (Player.IsCountdown)
			return;

		if (Player.IsTeleporting)
			return;

		if (path != null)
		{
			float triggerOffset = path.Curve.GetClosestOffset(GlobalPosition - path.GlobalPosition);
			float playerOffset = path.Curve.GetClosestOffset(Player.GlobalPosition - path.GlobalPosition);
			if (playerOffset > endPoint || playerOffset < triggerOffset)
			{
				isAutomationQueued = false;
				return;
			}
		}

		if (!ignoreDirection)
		{
			// Ensure character is facing/moving the correct direction
			float dot = ExtensionMethods.DotAngle(Player.MovementAngle, ExtensionMethods.CalculateForwardAngle(this.Forward()));
			if (dot < 0f || Player.IsMovingBackward)
				return;
		}

		isAutomationQueued = false;
		Player.StartAutomation(this);
	}

	public void Activate() => EmitSignal(SignalName.Activated);
	public void Deactivate() => EmitSignal(SignalName.Deactivated);

	public void OnEntered(Area3D _)
	{
		isAutomationQueued = autoQueueOnLand;
		isInteractingWithPlayer = true;
	}
	public void OnExited(Area3D _) => isInteractingWithPlayer = false;
}
