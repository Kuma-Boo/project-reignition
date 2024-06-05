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
		private int railLength = 5;
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
			rail.Curve = new();
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
		/// <summary> Is the player leaving via grindstep? </summary>
		private bool isGrindstepping;
		/// <summary> Can the player obtain bonuses on this rail? </summary>
		private bool allowBonuses = true;

		public override void _Ready()
		{
			if (Engine.IsEditorHint())
				return;

			// Create a path follower
			pathFollower = new()
			{
				UseModelFront = true,
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
		/// <summary> Basic measure for attaching at the end of the rail. </summary>
		private float RAIL_FUDGE_FACTOR => Skills.grindSettings.speed * PhysicsManager.physicsDelta;
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

			// Sync rail pathfollower
			Vector3 delta = rail.GlobalTransform.Basis.Inverse() * (Character.GlobalPosition - rail.GlobalPosition);
			pathFollower.Progress = rail.Curve.GetClosestOffset(delta);
			pathFollower.Progress = Mathf.Clamp(pathFollower.Progress, 0, rail.Curve.GetBakedLength() - RAIL_FUDGE_FACTOR);
			if (pathFollower.Progress >= rail.Curve.GetBakedLength() - RAIL_FUDGE_FACTOR) // Too close to the end of the rail; skip smoothing
				return;

			// Ignore grinds that would immediately put the player into a wall
			if (CheckWall(Skills.grindSettings.speed * PhysicsManager.physicsDelta)) return;

			delta = pathFollower.GlobalTransform.Basis.Inverse() * (Character.GlobalPosition - pathFollower.GlobalPosition);
			delta.Y -= Character.VerticalSpeed * PhysicsManager.physicsDelta;
			if (!Character.JustLandedOnGround && delta.Y < 0)
				return;

			// Horizontal validation
			if (Mathf.Abs(delta.X) > GRIND_RAIL_SNAPPING &&
				!(Character.ActionState == CharacterController.ActionStates.Grindstep && Mathf.Abs(delta.X) > GRINDSTEP_RAIL_SNAPPING))
				return;


			if (Character.Lockon.IsHomingAttacking) // Cancel any active homing attack
				Character.Lockon.StopHomingAttack();

			Activate(); // Start grinding
		}


		private void Activate()
		{
			isActive = true;
			isGrindstepping = false;

			if (allowBonuses && Character.IsGrindstepBonusActive)
				BonusManager.instance.QueueBonus(new(BonusType.Grindstep));

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

			float positionSmoothing = .2f;
			float smoothFactor = RAIL_FUDGE_FACTOR * 5f;
			if (pathFollower.Progress >= rail.Curve.GetBakedLength() - smoothFactor) // Calculate smoothing when activating at the end of the rail
			{
				float progressFactor = Mathf.Abs(pathFollower.Progress - rail.Curve.GetBakedLength());
				positionSmoothing = Mathf.SmoothStep(0f, positionSmoothing, Mathf.Clamp(progressFactor / smoothFactor, 0f, 1f));
			}
			Character.ResetActionState(); // Reset grind step, cancel stomps, jumps, etc
			Character.StartExternal(this, pathFollower, positionSmoothing);

			Character.IsMovingBackward = false;
			Character.LandOnGround(); // Rail counts as being on the ground
			Character.MoveSpeed = Skills.grindSettings.speed; // Start at the correct speed
			Character.VerticalSpeed = 0f;

			Character.Animator.ExternalAngle = 0; // Reset rotation
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
			allowBonuses = false;

			isFadingSFX = true; // Start fading sound effect
			grindParticles.Emitting = false; // Stop emitting particles
			if (isInvisibleRail) // Hide rail model
				railModel.Visible = false;

			Character.IsOnGround = false; // Disconnect from the ground
			Character.ResetMovementState();

			// Preserve speed
			float launchAngle = pathFollower.Up().AngleTo(Vector3.Up) * Mathf.Sign(pathFollower.Up().Y);
			Character.VerticalSpeed = Mathf.Sin(launchAngle) * -Character.MoveSpeed;
			Character.MoveSpeed = Mathf.Cos(launchAngle) * Character.MoveSpeed;

			if (!isGrindstepping)
				Character.Animator.ResetState(.2f);
			Character.Animator.SnapRotation(Character.MovementAngle);
			Character.Animator.IsFallTransitionEnabled = true;

			// Disconnect signals
			Character.Disconnect(CharacterController.SignalName.Knockback, new Callable(this, MethodName.Deactivate));
			Character.Disconnect(CharacterController.SignalName.ExternalControlCompleted, new Callable(this, MethodName.Deactivate));

			if (jumpBufferTimer > 0) // Player buffered a jump; allow cyote time
			{
				jumpBufferTimer = 0;
				Character.Jump(true);
			}

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
			Character.MovementAngle = ExtensionMethods.CalculateForwardAngle(pathFollower.Forward(), Character.PathFollower.Up());
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

				if (jumpBufferTimer > 0) //Jumping off rail can only happen when not shuffling
				{
					jumpBufferTimer = 0;

					//Check if the player is holding a direction parallel to rail and start a grindstep
					isGrindstepping = Character.IsHoldingDirection(Character.MovementAngle + Mathf.Pi * .5f) ||
						Character.IsHoldingDirection(Character.MovementAngle - Mathf.Pi * .5f);

					Deactivate();
					if (isGrindstepping) // Grindstepping
						Character.StartGrindstep();
					else // Jump normally
						Character.Jump(true);

					return;
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
			pathFollower.ProgressRatio = Mathf.Clamp(pathFollower.ProgressRatio, 0.0f, 1.0f);
			Character.UpdateExternalControl(true);
			Character.Animator.UpdateBalancing(Character.Animator.CalculateTurnRatio());
			Character.Animator.UpdateBalanceSpeed(Character.Skills.grindSettings.GetSpeedRatioClamped(Character.MoveSpeed));
			grindParticles.GlobalTransform = Character.GlobalTransform; // Sync particle position with player

			if (Mathf.IsEqualApprox(pathFollower.ProgressRatio, 1) || Mathf.IsZeroApprox(Character.MoveSpeed)) // Disconnect from the rail
				Deactivate();
		}


		private void StartShuffle(bool isPerfectShuffle)
		{
			shuffleBufferTimer = 0; // Reset input buffer

			Character.MoveSpeed = isPerfectShuffle ? Skills.perfectShuffleSpeed : Skills.grindSettings.speed;
			Character.Animator.StartGrindShuffle();

			if (!isPerfectShuffle)
				allowBonuses = false;

			if (allowBonuses)
				BonusManager.instance.QueueBonus(new(BonusType.GrindShuffle));
		}


		private RaycastHit CheckWall(float movementDelta)
		{
			float castLength = movementDelta + Character.CollisionRadius;
			RaycastHit hit = this.CastRay(pathFollower.GlobalPosition, pathFollower.Forward() * castLength, Character.CollisionMask);
			DebugManager.DrawRay(pathFollower.GlobalPosition, pathFollower.Forward() * castLength, hit ? Colors.Red : Colors.White);

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
