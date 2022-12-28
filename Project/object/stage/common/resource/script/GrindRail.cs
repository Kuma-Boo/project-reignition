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
			properties.Add(ExtensionMethods.CreateProperty("Align Camera", Variant.Type.Bool));

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
				case "Align Camera":
					return alignCamera;

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
				case "Align Camera":
					alignCamera = (bool)value;
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

		private bool alignCamera = true;
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
					Size = new Vector3(.5f, .5f, RailLength)
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
				isFadingSFX = SoundManager.instance.FadeSFX(sfx);

			if (isActive)
				UpdateRail();
			else if (isInteractingWithPlayer)
				CheckRailActivation();
		}

		private float chargeAmount;
		private readonly float GRIND_RAIL_CHARGE_LENGTH = .5f; //How long a full charge is.
		private readonly float GRIND_RAIL_SNAPPING = .5f; //How "magnetic" the rail is. Early 3D Sonic games tended to put this too low.
		private readonly float GRINDSTEP_RAIL_SNAPPING = 1.2f; //For when the player is stomping
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

			float horizontalOffset = Mathf.Abs(pathFollower.GetLocalPosition(Character.GlobalPosition).x); //Get local offset

			if (Character.VerticalSpd <= 0f)
			{
				if (horizontalOffset < GRIND_RAIL_SNAPPING ||
					(Character.IsGrindstepJump && horizontalOffset < GRINDSTEP_RAIL_SNAPPING)) //Start grinding
					ActivateRail();
			}
		}

		private void ActivateRail()
		{
			isActive = true;
			chargeAmount = 0;
			Character.IsGrindstepJump = false; //Reset grind step

			isFadingSFX = false;
			sfx.VolumeDb = 0f; //Reset volume
			sfx.Play();

			if (isInvisibleRail)
			{
				railModel.Visible = true;
				UpdateInvisibleRailPosition();
			}

			Character.ResetActionState(); //Cancel stomps, jumps, etc
			Character.StartExternal(this, pathFollower);

			Character.IsOnGround = true; //Rail counts as being on the ground
			Character.MoveSpeed = Skills.grindSettings.speed; //Start at the correct speed
			Character.VerticalSpd = 0f;

			Character.Animator.ExternalAngle = 0; //Rail modifies Character's Transform directly, animator angle is unused.
			Character.Animator.SnapRotation(Character.Animator.ExternalAngle); //Snap

			Character.Connect(CharacterController.SignalName.OnFinishedExternalControl, new Callable(this, MethodName.DisconnectFromRail), (uint)ConnectFlags.OneShot);
		}

		private void UpdateRail()
		{
			if (alignCamera) //Align the camera to follow movement direction
			{
				float targetDirection = Character.CalculateForwardAngle(pathFollower.Forward());
				Camera.OverrideYaw(targetDirection, .4f);
			}

			if (Character.MovementState != CharacterController.MovementStates.External ||
				Character.ExternalController != this) //Player must have disconnected from the rail
			{
				DisconnectFromRail();
				return;
			}

			//Delta angle to rail's movement direction (NOTE - Due to Godot conventions, negative is right, positive is left)
			float inputDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(Character.GetTargetInputAngle(), Character.MovementAngle);
			if (Controller.jumpButton.wasPressed)
			{
				//Check if the player is holding a direction parallel to rail.
				Character.IsGrindstepJump = !Character.IsHoldingDirection(Character.MovementAngle, true) && !Character.IsHoldingDirection(Character.MovementAngle + Mathf.Pi, true);
				if (Character.IsGrindstepJump) //Grindstep
				{
					//Calculate how far player is trying to go
					float horizontalTarget = GRIND_STEP_SPEED * Mathf.Sign(inputDeltaAngle);
					horizontalTarget *= Mathf.SmoothStep(0.5f, 1f, Controller.MovementAxisLength); //Give some smoothing based on controller strength

					Character.MovementAngle += Mathf.Pi * .5f * Mathf.Sign(inputDeltaAngle);
					Character.VerticalSpd = RuntimeConstants.GetJumpPower(GRIND_STEP_HEIGHT);
					Character.MoveSpeed = new Vector2(horizontalTarget, Character.MoveSpeed).Length();

					Character.IsOnGround = false; //Disconnect from the ground
					Character.CanJumpDash = false; //Disable jumpdashing
				}
				else //Jump normally
					Character.Jump(true);

				DisconnectFromRail();
				return;
			}

			Character.UpDirection = pathFollower.Up();
			Character.MovementAngle = Character.CalculateForwardAngle(pathFollower.Forward());
			Character.MoveSpeed = Skills.grindSettings.Interpolate(Character.MoveSpeed, 0f); //Slow down due to friction

			//TODO Disable the ability to shuffle accelerate during player's shuffle animation
			if (Controller.actionButton.isHeld) //Charge up!
			{
				chargeAmount = Mathf.MoveToward(chargeAmount, GRIND_RAIL_CHARGE_LENGTH, PhysicsManager.physicsDelta);
			}
			else
			{
				if (chargeAmount > 0f)
				{
					float t = Mathf.SmoothStep(0, 1, chargeAmount / GRIND_RAIL_CHARGE_LENGTH);
					Character.MoveSpeed = Mathf.Lerp(Skills.unchargedGrindSpeed, Skills.chargedGrindSpeed, t);
					sfx.Play();
				}

				chargeAmount = 0f;
			}

			if (isInvisibleRail)
				UpdateInvisibleRailPosition();

			//Check wall
			float movementDelta = Character.MoveSpeed * PhysicsManager.physicsDelta;
			RaycastHit hit = CheckWall(movementDelta);
			if (hit && hit.collidedObject is StaticBody3D) //Stop player when colliding with a static body
			{
				movementDelta = hit.distance; //Limit movement distance
				Character.MoveSpeed = 0f;
			}

			pathFollower.Progress += movementDelta;
			Character.UpdateExternalControl();

			if (pathFollower.ProgressRatio >= 1 || Mathf.IsZeroApprox(Character.MoveSpeed)) //Disconnect from the rail
				DisconnectFromRail();
		}

		private void DisconnectFromRail()
		{
			if (!isActive) return;

			isFadingSFX = true; //Start fading sound effect

			isActive = false;
			isInteractingWithPlayer = false;
			if (isInvisibleRail)
				railModel.Visible = false;

			Character.ResetMovementState();
			Character.Animator.SnapRotation(Character.MovementAngle);

			GD.Print($"{Character.MovementAngle}, {Character.Animator.Rotation.y}, {Character.Rotation.y}");

			//Disconnect signals
			if (Character.IsConnected(CharacterController.SignalName.OnFinishedExternalControl, new Callable(this, MethodName.DisconnectFromRail)))
				Character.Disconnect(CharacterController.SignalName.OnFinishedExternalControl, new Callable(this, MethodName.DisconnectFromRail));
		}

		private RaycastHit CheckWall(float movementDelta)
		{
			float castLength = movementDelta + Character.CollisionRadius;
			RaycastHit hit = this.CastRay(pathFollower.GlobalPosition, pathFollower.Forward() * castLength, Character.CollisionMask);
			Debug.DrawRay(pathFollower.GlobalPosition, pathFollower.Forward() * castLength, hit ? Colors.Red : Colors.White);
			return hit;
		}

		private void UpdateInvisibleRailPosition()
		{
			railModel.GlobalPosition = Character.GlobalPosition;
			railModel.Position = new Vector3(0, railModel.Position.y, railModel.Position.z); //Ignore player's x-offset
			railMaterial.Set("uv_offset", railModel.Position.z % 1);
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
