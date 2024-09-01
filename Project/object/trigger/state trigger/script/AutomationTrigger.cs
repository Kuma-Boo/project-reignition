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
	[Export]
	private float endPoint;
	/// <summary> Always activate regardless of which way the player entered/moves. </summary>
	[Export]
	private bool ignoreDirection;
	private bool isInteractingWithPlayer;
	private PlayerController Player => StageSettings.Player;
	public bool IsFinished => Player.PathFollower.Progress >= endPoint;

	public override void _PhysicsProcess(double _)
	{
		if (!isInteractingWithPlayer)
			return;

		AttemptAutomation();
	}

	private void AttemptAutomation()
	{
		if (Player.IsAutomationActive || !Player.IsOnGround)
			return;

		if (!ignoreDirection)
		{
			// Ensure character is facing/moving the correct direction
			float dot = ExtensionMethods.DotAngle(Player.MovementAngle, ExtensionMethods.CalculateForwardAngle(this.Forward()));
			if (dot < 0f || Player.IsMovingBackward)
				return;
		}

		Player.StartAutomation(this);
	}

	public void Activate() => EmitSignal(SignalName.Activated);
	public void Deactivate() => EmitSignal(SignalName.Deactivated);

	public void OnEntered(Area3D _) => isInteractingWithPlayer = true;
	public void OnExited(Area3D _) => isInteractingWithPlayer = false;
}
