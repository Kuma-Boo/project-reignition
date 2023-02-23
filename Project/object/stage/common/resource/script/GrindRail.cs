using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Object that controls how grinding works. Keep in mind grinding backwards isn't supported.
	/// </summary>
	[Tool]
	public partial class GrindRail : Area3D
	{
		#region Editor
		public override Array<Dictionary> _GetPropertyList()
		{
			Array<Dictionary> properties = new Array<Dictionary>();

			properties.Add(ExtensionMethods.CreateProperty("Rail Path", Variant.Type.NodePath, PropertyHint.NodePathValidTypes, "Path3D"));
			properties.Add(ExtensionMethods.CreateProperty("Invisible Rail Settings/Enabled", Variant.Type.Bool));

			if (isInvisibleRail)
			{
				properties.Add(ExtensionMethods.CreateProperty("Invisible Rail Settings/Length", Variant.Type.Int, PropertyHint.Range, "5,120"));

				properties.Add(ExtensionMethods.CreateProperty("Invisible Rail Settings/Rail Object", Variant.Type.NodePath, PropertyHint.NodePathValidTypes, "Node3D"));
				properties.Add(ExtensionMethods.CreateProperty("Invisible Rail Settings/Rail Material", Variant.Type.Object));
				properties.Add(ExtensionMethods.CreateProperty("Invisible Rail Settings/Start Cap", Variant.Type.NodePath, PropertyHint.NodePathValidTypes, "Node3D"));
				properties.Add(ExtensionMethods.CreateProperty("Invisible Rail Settings/End Cap", Variant.Type.NodePath, PropertyHint.NodePathValidTypes, "Node3D"));
				properties.Add(ExtensionMethods.CreateProperty("Invisible Rail Settings/Collider", Variant.Type.NodePath, PropertyHint.NodePathValidTypes, "CollisionShape3D"));
			}

			return properties;
		}

		public override Variant _Get(StringName property)
		{
			switch ((string)property)
			{
				case "Rail Path":
					return railPath;

				case "Invisible Rail Settings/Enabled":
					return isInvisibleRail;
				case "Invisible Rail Settings/Length":
					return RailLength;

				case "Invisible Rail Settings/Rail Object":
					return railModelPath;
				case "Invisible Rail Settings/Rail Material":
					return railMaterial;

				case "Invisible Rail Settings/Start Cap":
					return startCapPath;
				case "Invisible Rail Settings/End Cap":
					return endCapPath;
				case "Invisible Rail Settings/Collider":
					return colliderPath;
			}

			return base._Get(property);
		}

		public override bool _Set(StringName property, Variant value)
		{
			switch ((string)property)
			{
				case "Rail Path":
					railPath = (NodePath)value;
					break;

				case "Invisible Rail Settings/Enabled":
					isInvisibleRail = (bool)value;
					NotifyPropertyListChanged();
					break;
				case "Invisible Rail Settings/Length":
					RailLength = (int)value;
					break;


				case "Invisible Rail Settings/Rail Object":
					railModelPath = (NodePath)value;
					break;
				case "Invisible Rail Settings/Rail Material":
					railMaterial = (Material)value;
					break;

				case "Invisible Rail Settings/Start Cap":
					startCapPath = (NodePath)value;
					break;
				case "Invisible Rail Settings/End Cap":
					endCapPath = (NodePath)value;
					break;
				case "Invisible Rail Settings/Collider":
					colliderPath = (NodePath)value;
					break;
				default:
					return false;
			}

			return true;
		}
		#endregion

		[Signal]
		public delegate void GrindStartedEventHandler();
		[Signal]
		public delegate void GrindCompletedEventHandler();

		private Path3D rail;
		private NodePath railPath;
		private PathFollow3D pathFollower;

		private NodePath colliderPath;
		private NodePath railModelPath;
		private NodePath startCapPath;
		private NodePath endCapPath;

		private Node3D railModel;
		private Material railMaterial;
		private Node3D startCap;
		private Node3D endCap;
		private CollisionShape3D collider;
		public int RailLength { get; set; } //Only used on invisible rails
		private bool isInvisibleRail;

		[Export]
		private AudioStreamPlayer sfx;
		private bool isFadingSFX;

		private bool isActive; //Is the rail active?
		private bool isInteractingWithPlayer; //Check for collisions?

		private CameraController Camera => CameraController.instance;
		private CharacterController Character => CharacterController.instance;
		private CharacterSkillManager Skills => Character.Skills;
		private InputManager.Controller Controller => InputManager.controller;

		private readonly float GRIND_STEP_HEIGHT = 1.6f; //How high to jump during a grindstep
		private readonly float GRIND_STEP_SPEED = 24.0f; //How fast to move during a grindstep

		public override void _Ready()
		{
			if (Engine.IsEditorHint())
				return;

			pathFollower = new PathFollow3D()
			{
				Loop = false,
				CubicInterp = false,
				RotationMode = PathFollow3D.RotationModeEnum.Oriented
			};

			rail = GetNode<Path3D>(railPath);
			rail.CallDeferred("add_child", pathFollower);

			if (isInvisibleRail) //For Secret Rings' hidden rails
			{
				UpdateInvisibleRailLength();

				collider = GetNode<CollisionShape3D>(colliderPath);
				railModel = GetNode<Node3D>(railModelPath);

				railModel.Visible = false;

				//Generate curve and collision
				collider.Shape = new BoxShape3D()
				{
					Size = new Vector3(2f, .5f, RailLength)
				};
				collider.Position = Vector3.Forward * RailLength * .5f + Vector3.Down * .05f;
				rail.Curve = new Curve3D();
				rail.Curve.AddPoint(Vector3.Zero, null, Vector3.Forward);
				rail.Curve.AddPoint(Vector3.Forward * RailLength, Vector3.Back);
			}
		}

		public override void _PhysicsProcess(double _)
		{
			if (Engine.IsEditorHint())
			{
				UpdateInvisibleRailLength();
				return;
			}

			if (isFadingSFX)
				isFadingSFX = SoundManager.FadeSFX(sfx);

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
		private const float GRIND_RAIL_SNAPPING = .6f;
		/// <summary> Rail snapping is more generous when performing a grind step. </summary>
		private const float GRINDSTEP_RAIL_SNAPPING = 1.4f;
		private Vector3 closestPoint;
		private void CheckRailActivation()
		{
			if (Character.IsOnGround && !Character.JustLandedOnGround) return; //Can't start grinding from the ground
			if (Character.Lockon.IsHomingAttacking) return; //Character is targeting something
			if (Character.MovementState != CharacterController.MovementStates.Normal) return; //Character is busy

			//Sync rail pathfollower
			Vector3 delta = rail.GetLocalPosition(Character.GlobalPosition);
			pathFollower.Progress = rail.Curve.GetClosestOffset(delta);

			//Check walls
			if (CheckWall(Skills.grindSettings.speed * PhysicsManager.physicsDelta)) return;

			float horizontalOffset = Mathf.Abs(pathFollower.GetLocalPosition(Character.GlobalPosition).X); //Get local offset

			if (Character.VerticalSpd <= 0f)
			{
				if (horizontalOffset < GRIND_RAIL_SNAPPING ||
					(Character.IsGrindstepping && horizontalOffset < GRINDSTEP_RAIL_SNAPPING)) //Start grinding
					ActivateRail();
			}
		}

		private void ActivateRail()
		{
			isActive = true;
			jumpBufferTimer = 0;
			Character.ResetActionState(); //Reset grind step

			isFadingSFX = false;
			sfx.VolumeDb = 0f; //Reset volume
			sfx.Play();

			if (isInvisibleRail)
			{
				railModel.Visible = true;
				UpdateInvisibleRailPosition();
			}

			shuffleBufferTimer = 0; //Reset buffer timer

			Character.Skills.IsSpeedBreakEnabled = false;
			Character.ResetActionState(); //Cancel stomps, jumps, etc
			Character.StartExternal(this, pathFollower);

			Character.IsMovingBackward = false;
			Character.LandOnGround(); //Rail counts as being on the ground
			Character.MoveSpeed = Skills.grindSettings.speed; //Start at the correct speed
			Character.VerticalSpd = 0f;

			Character.Animator.ExternalAngle = 0; //Rail modifies Character's Transform directly, animator angle is unused.
			Character.Animator.StartBalancing();
			Character.Animator.SnapRotation(Character.Animator.ExternalAngle); //Snap
			Character.Effect.StartGrindrail(); //Start creating sparks

			Character.Connect(CharacterController.SignalName.Knockback, new Callable(this, MethodName.DisconnectFromRail));
			Character.Connect(CharacterController.SignalName.ExternalControlCompleted, new Callable(this, MethodName.DisconnectFromRail));

			EmitSignal(SignalName.GrindStarted);
		}

		private void UpdateRail()
		{
			if (Character.MovementState != CharacterController.MovementStates.External ||
				Character.ExternalController != this) //Player must have disconnected from the rail
			{
				DisconnectFromRail();
				return;
			}

			float railAngle = Character.CalculateForwardAngle(pathFollower.Forward());

			Character.UpDirection = pathFollower.Up();
			Character.MovementAngle = railAngle;
			Character.MoveSpeed = Skills.grindSettings.Interpolate(Character.MoveSpeed, 0f); //Slow down due to friction

			sfx.VolumeDb = -9f * Mathf.SmoothStep(0, 1, 1 - Skills.grindSettings.GetSpeedRatioClamped(Character.MoveSpeed)); //Fade volume based on speed

			//Update shuffling
			if (Controller.actionButton.wasPressed)
			{
				if (!Character.Animator.IsBalanceShuffleActive)
					StartShuffle(false); //Shuffle was performed slightly late
				else if (Mathf.IsZeroApprox(shuffleBufferTimer))
					shuffleBufferTimer = SHUFFLE_BUFFER_LENGTH;
				else //Don't allow button mashers
					shuffleBufferTimer = -SHUFFLE_BUFFER_LENGTH;
			}

			if (Controller.jumpButton.wasPressed)
				jumpBufferTimer = JUMP_BUFFER_LENGTH;

			shuffleBufferTimer = Mathf.MoveToward(shuffleBufferTimer, 0, PhysicsManager.physicsDelta);
			jumpBufferTimer = Mathf.MoveToward(jumpBufferTimer, 0, PhysicsManager.physicsDelta);

			if (!Character.Animator.IsBalanceShuffleActive)
			{
				if (shuffleBufferTimer > 0)
					StartShuffle(true);

				shuffleBufferTimer = 0;

				if (jumpBufferTimer > 0) //Jumping off rail can only happen when not shuffling
				{
					jumpBufferTimer = 0;

					//Check if the player is holding a direction parallel to rail and perform a grindstep
					if (Character.IsHoldingDirection(railAngle + Mathf.Pi * .5f) ||
						Character.IsHoldingDirection(railAngle - Mathf.Pi * .5f))
						Character.StartGrindstep();

					DisconnectFromRail();

					if (Character.IsGrindstepping) //Grindstep
					{
						//Delta angle to rail's movement direction (NOTE - Due to Godot conventions, negative is right, positive is left)
						float inputDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(Character.GetTargetInputAngle(), railAngle);
						//Calculate how far player is trying to go
						float horizontalTarget = GRIND_STEP_SPEED * Mathf.Sign(inputDeltaAngle);
						horizontalTarget *= Mathf.SmoothStep(0.5f, 1f, Controller.MovementAxisLength); //Give some smoothing based on controller strength

						//Keep some speed forward
						Character.MovementAngle += Mathf.Pi * .25f * Mathf.Sign(inputDeltaAngle);
						Character.VerticalSpd = Runtime.GetJumpPower(GRIND_STEP_HEIGHT);
						Character.MoveSpeed = new Vector2(horizontalTarget, Character.MoveSpeed).Length();

						Character.IsOnGround = false; //Disconnect from the ground
						Character.CanJumpDash = false; //Disable jumpdashing

						Character.Animator.StartGrindStep();
					}
					else //Jump normally
						Character.Jump(true);
				}
			}

			if (isInvisibleRail)
				UpdateInvisibleRailPosition();

			//Check wall
			float movementDelta = Character.MoveSpeed * PhysicsManager.physicsDelta;
			RaycastHit hit = CheckWall(movementDelta);
			if (hit && hit.collidedObject is StaticBody3D) //Stop player when colliding with a static body
			{
				movementDelta = 0; //Limit movement distance
				Character.MoveSpeed = 0f;
			}
			else //No walls, Check for crushers
				Character.CheckCeiling();

			pathFollower.Progress += movementDelta;
			Character.UpdateExternalControl(true);
			Character.Animator.UpdateBalancing();

			if (pathFollower.ProgressRatio >= 1 || Mathf.IsZeroApprox(Character.MoveSpeed)) //Disconnect from the rail
				DisconnectFromRail();
		}

		private void StartShuffle(bool isPerfectShuffle)
		{
			shuffleBufferTimer = 0; //Reset input buffer

			LevelSettings.instance.AddBonus(isPerfectShuffle ? LevelSettings.BonusType.PerfectGrindShuffle : LevelSettings.BonusType.GrindShuffle);

			Character.MoveSpeed = isPerfectShuffle ? Skills.perfectShuffleSpeed : Skills.grindSettings.speed;
			Character.Animator.StartGrindShuffle();
		}

		private void DisconnectFromRail()
		{
			if (!isActive) return;

			isFadingSFX = true; //Start fading sound effect

			if (jumpBufferTimer > 0) //Player buffered jump late; cyote time
				Character.Jump(true);

			isActive = false;
			isInteractingWithPlayer = false;
			if (isInvisibleRail)
				railModel.Visible = false;

			Character.Skills.IsSpeedBreakEnabled = true;
			Character.ResetMovementState();

			if (!Character.IsGrindstepping)
				Character.Animator.ResetState(.2f);
			Character.Animator.SnapRotation(Character.MovementAngle);
			Character.Effect.StopGrindrail(); //Stop creating sparks

			//Disconnect signals
			if (Character.IsConnected(CharacterController.SignalName.Knockback, new Callable(this, MethodName.DisconnectFromRail)))
				Character.Disconnect(CharacterController.SignalName.Knockback, new Callable(this, MethodName.DisconnectFromRail));
			if (Character.IsConnected(CharacterController.SignalName.ExternalControlCompleted, new Callable(this, MethodName.DisconnectFromRail)))
				Character.Disconnect(CharacterController.SignalName.ExternalControlCompleted, new Callable(this, MethodName.DisconnectFromRail));

			EmitSignal(SignalName.GrindCompleted);
		}

		private RaycastHit CheckWall(float movementDelta)
		{
			float castLength = movementDelta + Character.CollisionRadius;
			RaycastHit hit = this.CastRay(pathFollower.GlobalPosition, pathFollower.Forward() * castLength, Character.CollisionMask);
			Debug.DrawRay(pathFollower.GlobalPosition, pathFollower.Forward() * castLength, hit ? Colors.Red : Colors.White);

			//Allow grindrails to travel through certain walls
			if (hit && hit.collidedObject.IsInGroup("allow grindrail")) return new RaycastHit();

			return hit;
		}

		private void UpdateInvisibleRailPosition()
		{
			railModel.GlobalPosition = Character.GlobalPosition;
			railModel.Position = new Vector3(0, railModel.Position.Y, railModel.Position.Z); //Ignore player's x-offset
			railMaterial.Set("uv_offset", railModel.Position.Z % 1);
		}

		private void UpdateInvisibleRailLength()
		{
			startCap = GetNodeOrNull<Node3D>(startCapPath);
			endCap = GetNodeOrNull<Node3D>(endCapPath);

			if (startCap != null)
				startCap.Position = Vector3.Forward;

			if (endCap != null)
				endCap.Position = Vector3.Forward * (RailLength - 1);
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

			DisconnectFromRail();
		}
	}
}
