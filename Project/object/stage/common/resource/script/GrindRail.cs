using Godot;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Handles grinding. Backwards grinding isn't supported.
	/// </summary>
	[Tool]
	public partial class GrindRail : Area3D
	{
		[Signal]
		public delegate void GrindStartedEventHandler();
		[Signal]
		public delegate void GrindCompletedEventHandler();


		[ExportGroup("Components")]
		[Export]
		private Path3D rail;
		[Export]
		private AudioStreamPlayer sfx;
		private bool isFadingSFX;
		[Export]
		private GpuParticles3D grindParticles;
		/// <summary> Reference to the grindrail's pathfollower. </summary>
		private PathFollow3D pathFollower;
		private CharacterController Character => CharacterController.instance;
		private CharacterSkillManager Skills => Character.Skills;


		[ExportGroup("Invisible Rail Settings")]
		[Export]
		private bool isInvisibleRail;
		[Export]
		private Node3D railModel;
		[Export]
		private ShaderMaterial railMaterial;
		[Export]
		private NodePath startCapPath;
		private Node3D startCap;
		[Export]
		private NodePath endCapPath;
		private Node3D endCap;
		[Export]
		private CollisionShape3D collider;
		[Export(PropertyHint.Range, "5, 120")]
		/// <summary> Only used for invisible rails. </summary>
		private int railLength;
		/// <summary> Updates rail's visual length. </summary>
		private void UpdateInvisibleRailLength()
		{
			startCap = GetNodeOrNull<Node3D>(startCapPath);
			endCap = GetNodeOrNull<Node3D>(endCapPath);

			if (startCap != null)
				startCap.Position = Vector3.Forward;

			if (endCap != null)
				endCap.Position = Vector3.Forward * (railLength - 1);
		}


		/// <summary> Generates rail's collision and curve. </summary>
		private void InitializeInvisibleRail()
		{
			UpdateInvisibleRailLength();
			railModel.Visible = false;

			// Generate collision and curve
			collider.Shape = new BoxShape3D()
			{
				Size = new Vector3(2f, .5f, railLength)
			};
			collider.Position = Vector3.Forward * railLength * .5f + Vector3.Down * .05f;
			rail.Curve = new Curve3D();
			rail.Curve.AddPoint(Vector3.Zero, null, Vector3.Forward);
			rail.Curve.AddPoint(Vector3.Forward * railLength, Vector3.Back);
		}


		/// <summary> Updates invisible rails to sync with the player's position. </summary>
		private void UpdateInvisibleRailPosition()
		{
			railModel.GlobalPosition = Character.GlobalPosition;
			railModel.Position = new Vector3(0, railModel.Position.Y, railModel.Position.Z); // Ignore player's x-offset
			railMaterial.SetShaderParameter("uv_offset", railModel.Position.Z);
		}


		/// <summary> Is the rail active? </summary>
		private bool isActive;
		/// <summary> Process collisions? </summary>
		private bool isInteractingWithPlayer;

		/// <summary> How high to jump during a grindstep. </summary>
		private readonly float GRIND_STEP_HEIGHT = 1.6f;
		/// <summary> How fast to move during a grindstep. </summary>
		private readonly float GRIND_STEP_SPEED = 24.0f;


		public override void _Ready()
		{
			if (Engine.IsEditorHint())
				return;

			// Create a path follower
			pathFollower = new PathFollow3D()
			{
				Loop = false,
			};

			rail.CallDeferred("add_child", pathFollower);

			// For Secret Rings' hidden rails
			if (isInvisibleRail)
				InitializeInvisibleRail();
		}


		public override void _PhysicsProcess(double _)
		{
			if (Engine.IsEditorHint())
			{
				UpdateInvisibleRailLength();
				return;
			}

			if (isFadingSFX)
				isFadingSFX = SoundManager.FadeAudioPlayer(sfx);

			if (isActive)
				UpdateRail();
			else if (isInteractingWithPlayer)
				CheckRailActivation();
		}


		/// <summary> Is a jump currently being buffered? </summary>
		private float jumpBufferTimer;
		/// <summary> Is a shuffle currently being buffered? </summary>
		private float shuffleBufferTimer;
		private const float JUMP_BUFFER_LENGTH = .2f;
		private const float SHUFFLE_BUFFER_LENGTH = .4f;
		/// <summary> How "magnetic" the rail is. Early 3D Sonic games had a habit of putting this too low. </summary>
		private const float GRIND_RAIL_SNAPPING = 1.0f;
		/// <summary> Rail snapping is more generous when performing a grind step. </summary>
		private const float GRINDSTEP_RAIL_SNAPPING = 1.4f;
		private Vector3 closestPoint;
		private void CheckRailActivation()
		{
			if (Character.IsOnGround && !Character.JustLandedOnGround) return; // Can't start grinding from the ground
			if (Character.MovementState != CharacterController.MovementStates.Normal) return; // Character is busy
			if (Character.VerticalSpeed > 0f) return; // Player must be falling to start grinding!

			// Cancel any active homing attack
			if (Character.Lockon.IsHomingAttacking)
				Character.Lockon.StopHomingAttack();

			// Sync rail pathfollower
			Vector3 delta = rail.GlobalTransform.Basis.Inverse() * (Character.GlobalPosition - rail.GlobalPosition);
			pathFollower.Progress = rail.Curve.GetClosestOffset(delta);

			// Ignore grinds that would immediately put the player into a wall
			if (CheckWall(Skills.grindSettings.speed * PhysicsManager.physicsDelta)) return;

			delta = pathFollower.GlobalTransform.Basis.Inverse() * (Character.GlobalPosition - pathFollower.GlobalPosition);
			if (Mathf.Abs(delta.X) < GRIND_RAIL_SNAPPING ||
				(Character.IsGrindstepping && Mathf.Abs(delta.X) < GRINDSTEP_RAIL_SNAPPING)) // Start grinding
				Activate();
		}


		private void Activate()
		{
			isActive = true;

			// Reset buffer timers
			jumpBufferTimer = 0;
			shuffleBufferTimer = 0;

			// Reset FX
			isFadingSFX = false;
			sfx.VolumeDb = 0f;
			sfx.Play();
			grindParticles.Emitting = true; // Start emitting sparks

			// Show invisible grindrail
			if (isInvisibleRail)
			{
				railModel.Visible = true;
				UpdateInvisibleRailPosition();
			}

			Character.ResetActionState(); // Reset grind step, cancel stomps, jumps, etc
			Character.StartExternal(this, pathFollower);

			Character.IsMovingBackward = false;
			Character.LandOnGround(); // Rail counts as being on the ground
			Character.MoveSpeed = Skills.grindSettings.speed; // Start at the correct speed
			Character.VerticalSpeed = 0f;

			Character.Animator.ExternalAngle = Mathf.Pi; // Rotate to follow pathfollower
			Character.Animator.StartBalancing();
			Character.Animator.SnapRotation(Character.Animator.ExternalAngle);

			Character.Connect(CharacterController.SignalName.Knockback, new Callable(this, MethodName.Deactivate));
			Character.Connect(CharacterController.SignalName.ExternalControlCompleted, new Callable(this, MethodName.Deactivate));

			UpdateRail();
			EmitSignal(SignalName.GrindStarted);
		}


		private void Deactivate()
		{
			if (!isActive) return;
			isActive = false;
			isInteractingWithPlayer = false;

			isFadingSFX = true; // Start fading sound effect
			grindParticles.Emitting = false; // Stop emitting particles
			if (isInvisibleRail) // Hide rail model
				railModel.Visible = false;

			if (jumpBufferTimer > 0) // Player buffered a jump; allow cyote time
				Character.Jump(true);

			Character.IsOnGround = false; // Disconnect from the ground
			Character.ResetMovementState();

			// Preserve speed
			float launchAngle = pathFollower.Up().AngleTo(Vector3.Up) * Mathf.Sign(pathFollower.Up().Y);
			Character.VerticalSpeed = Mathf.Sin(launchAngle) * -Character.MoveSpeed;
			Character.MoveSpeed = Mathf.Cos(launchAngle) * Character.MoveSpeed;

			if (!Character.IsGrindstepping)
				Character.Animator.ResetState(.2f);
			Character.Animator.SnapRotation(Character.MovementAngle);

			// Disconnect signals
			Character.Disconnect(CharacterController.SignalName.Knockback, new Callable(this, MethodName.Deactivate));
			Character.Disconnect(CharacterController.SignalName.ExternalControlCompleted, new Callable(this, MethodName.Deactivate));

			EmitSignal(SignalName.GrindCompleted);
		}


		private void UpdateRail()
		{
			if (Character.MovementState != CharacterController.MovementStates.External ||
				Character.ExternalController != this) // Player disconnected from the rail already
			{
				Deactivate();
				return;
			}

			Character.UpDirection = pathFollower.Up();
			Character.MovementAngle = Character.CalculateForwardAngle(pathFollower.Back());
			Character.MoveSpeed = Skills.grindSettings.Interpolate(Character.MoveSpeed, 0f); //Slow down due to friction

			sfx.VolumeDb = -9f * Mathf.SmoothStep(0, 1, 1 - Skills.grindSettings.GetSpeedRatioClamped(Character.MoveSpeed)); //Fade volume based on speed

			// Update shuffling
			if (Input.IsActionJustPressed("button_action"))
			{
				if (!Character.Animator.IsBalanceShuffleActive)
					StartShuffle(false); // Shuffle was performed slightly late
				else if (Mathf.IsZeroApprox(shuffleBufferTimer))
					shuffleBufferTimer = SHUFFLE_BUFFER_LENGTH;
				else // Don't allow button mashers
					shuffleBufferTimer = -SHUFFLE_BUFFER_LENGTH;
			}

			if (Input.IsActionJustPressed("button_jump"))
				jumpBufferTimer = JUMP_BUFFER_LENGTH;

			shuffleBufferTimer = Mathf.MoveToward(shuffleBufferTimer, 0, PhysicsManager.physicsDelta);
			jumpBufferTimer = Mathf.MoveToward(jumpBufferTimer, 0, PhysicsManager.physicsDelta);

			if (!Character.Animator.IsBalanceShuffleActive)
			{
				if (shuffleBufferTimer > 0) // Shuffle was buffered perfectly
					StartShuffle(true);

				shuffleBufferTimer = 0;

				if (jumpBufferTimer > 0) //Jumping off rail can only happen when not shuffling
				{
					jumpBufferTimer = 0;

					//Check if the player is holding a direction parallel to rail and start a grindstep
					if (Character.IsHoldingDirection(Character.MovementAngle + Mathf.Pi * .5f) ||
						Character.IsHoldingDirection(Character.MovementAngle - Mathf.Pi * .5f))
						Character.StartGrindstep();

					Deactivate();
					if (Character.IsGrindstepping) // Grindstepping
						StartGrindstep();
					else // Jump normally
						Character.Jump(true);
				}
			}

			if (isInvisibleRail)
				UpdateInvisibleRailPosition();

			// Check wall
			float movementDelta = Character.MoveSpeed * PhysicsManager.physicsDelta;
			RaycastHit hit = CheckWall(movementDelta);
			if (hit && hit.collidedObject is StaticBody3D) // Stop player when colliding with a static body
			{
				movementDelta = 0; // Limit movement distance
				Character.MoveSpeed = 0f;
			}
			else // No walls, Check for crushers
				Character.CheckCeiling();

			pathFollower.Progress += movementDelta;
			Character.UpdateExternalControl(true);
			Character.Animator.UpdateBalancing(Character.Animator.CalculateTurnRatio());
			Character.Animator.UpdateBalanceSpeed(Character.Skills.grindSettings.GetSpeedRatioClamped(Character.MoveSpeed));
			grindParticles.GlobalTransform = Character.GlobalTransform; // Sync particle position with player

			if (pathFollower.ProgressRatio >= 1 || Mathf.IsZeroApprox(Character.MoveSpeed)) // Disconnect from the rail
				Deactivate();
		}


		private void StartGrindstep()
		{
			// Delta angle to rail's movement direction (NOTE - Due to Godot conventions, negative is right, positive is left)
			float inputDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(Character.GetInputAngle(), Character.MovementAngle);
			// Calculate how far player is trying to go
			float horizontalTarget = GRIND_STEP_SPEED * Mathf.Sign(inputDeltaAngle);
			horizontalTarget *= Mathf.SmoothStep(0.5f, 1f, Character.InputVector.Length()); // Give some smoothing based on controller strength

			// Keep some speed forward
			Character.MovementAngle += Mathf.Pi * .25f * Mathf.Sign(inputDeltaAngle);
			Character.VerticalSpeed = Runtime.CalculateJumpPower(GRIND_STEP_HEIGHT);
			Character.MoveSpeed = new Vector2(horizontalTarget, Character.MoveSpeed).Length();

			Character.CanJumpDash = false; // Disable jumpdashing
			Character.Animator.StartGrindStep();
		}


		private void StartShuffle(bool isPerfectShuffle)
		{
			shuffleBufferTimer = 0; // Reset input buffer

			StageSettings.instance.AddBonus(isPerfectShuffle ? StageSettings.BonusType.PerfectGrindShuffle : StageSettings.BonusType.GrindShuffle);

			Character.MoveSpeed = isPerfectShuffle ? Skills.perfectShuffleSpeed : Skills.grindSettings.speed;
			Character.Animator.StartGrindShuffle();
		}


		private RaycastHit CheckWall(float movementDelta)
		{
			float castLength = movementDelta + Character.CollisionRadius;
			RaycastHit hit = this.CastRay(pathFollower.GlobalPosition, pathFollower.Back() * castLength, Character.CollisionMask);
			Debug.DrawRay(pathFollower.GlobalPosition, pathFollower.Back() * castLength, hit ? Colors.Red : Colors.White);

			// Block grinding through objects in the given group
			if (hit && hit.collidedObject.IsInGroup("grind wall"))
				return hit;

			return new RaycastHit();
		}


		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			isInteractingWithPlayer = true;
			CheckRailActivation();
		}


		public void OnExited(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			isInteractingWithPlayer = false;

			Deactivate();
		}
	}
}
