using Godot;
using Godot.Collections;

namespace Project.Gameplay.Objects;

[Tool]
public partial class LaunchRing : Launcher
{
	[Signal]
	public delegate void EnteredEventHandler();
	[Signal]
	public delegate void ExitedEventHandler();

	[ExportGroup("Editor")]
	[Export]
	private Array<NodePath> pieces;
	private readonly Array<Node3D> _pieces = [];
	private readonly int PieceCount = 16;
	private readonly float RingSize = 2.2f;

	/// <summary> Is this the spike variant? </summary>
	[Export]
	private bool isSpikeVariant;
	[Export]
	private AnimationPlayer animator;
	[Export]
	private AudioStreamPlayback sfx;
	private bool isActive;

	public override float GetLaunchRatio() => isSpikeVariant ? 1f : Mathf.SmoothStep(0, 1, launchRatio);

	public override void _Ready()
	{
		if (Engine.IsEditorHint()) return;

		InitializePieces();
	}

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint() && pieces.Count != _pieces.Count)
			InitializePieces();

		UpdatePieces();

		if (Engine.IsEditorHint()) return;

		if (isActive)
		{
			// Recenter player
			Character.CenterPosition = RecenterCharacter();

			if (IsCharacterCentered) // Close enough; Allow inputs
			{
				if (Input.IsActionJustPressed("button_jump")) // Disable launcher
				{
					DropPlayer(false);
					Character.CanJumpDash = true;
				}
				else if (Input.IsActionJustPressed("button_action"))
				{
					LaunchPlayer();
				}
			}

			Character.Animator.SetSpinSpeed(1.5f + launchRatio);
		}
	}

	protected override void LaunchAnimation()
	{
		// Keep the same animation as charging (i.e. do nothing)
		Character.Animator.SetSpinSpeed(5); // Speed up spin animation just because
	}

	private void DropPlayer(bool launched = false)
	{
		isActive = false;
		Character.ResetMovementState();

		if (!launched)
		{
			EmitSignal(SignalName.Exited);
			Character.Animator.ResetState();
			Character.Effect.StopSpinFX();
			Character.CanJumpDash = false;
		}
	}

	private void LaunchPlayer()
	{
		DropPlayer(true);
		Character.Effect.StartTrailFX();
		base.Activate();
	}

	private void InitializePieces()
	{
		for (int i = 0; i < pieces.Count; i++)
			_pieces.Add(GetNode<Node3D>(pieces[i]));
	}

	private void UpdatePieces()
	{
		if (_pieces.Count == 0) return;

		float interval = Mathf.Tau / PieceCount;
		for (int i = 0; i < _pieces.Count; i++)
		{
			if (_pieces[i] == null) continue;

			Vector3 movementVector = -Vector3.Up.Rotated(Vector3.Forward, interval * (i + .5f)); // Offset rotation slightly, since visual model is offset
			_pieces[i].Position = movementVector * launchRatio * RingSize;
		}
	}

	private void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;

		animator.Play("charge");
		Character.StartExternal(this);
		Character.Animator.StartSpin();
		Character.Effect.StartSpinFX();

		isActive = true;
		Character.MovementAngle = ExtensionMethods.CalculateForwardAngle(this.Forward().RemoveVertical().Normalized());
		Character.Animator.ExternalAngle = Character.MovementAngle;

		// Disable homing reticle
		Character.Lockon.IsMonitoring = false;
		Character.Lockon.StopHomingAttack();
		EmitSignal(SignalName.Entered);
	}

	private void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;
		animator.Play("RESET", .2 * (1 + launchRatio));
	}

	public void DamagePlayer()
	{
		DropPlayer();
		Character.StartKnockback(new CharacterController.KnockbackSettings()
		{
			ignoreMovementState = true,
		});
	}
}