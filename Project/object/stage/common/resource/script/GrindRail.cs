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
					return railPathPath;

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
					railPathPath = (NodePath)value;
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

		private Path3D railPath;
		private NodePath railPathPath;
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

		private bool isActive; //Is the rail active?
		private bool isInteractingWithPlayer; //Check for collisions?

		private CharacterController Character => CharacterController.instance;
		private CharacterSkillManager Skills => Character.Skills;
		private InputManager.Controller Controller => InputManager.controller;

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

			railPath = GetNode<Path3D>(railPathPath);
			railPath.CallDeferred("add_child", pathFollower);
			if (isInvisibleRail) //For Secret Rings' hidden rails
			{
				UpdateInvisibleRailLength();

				collider = GetNode<CollisionShape3D>(colliderPath);
				railModel = GetNode<Node3D>(railModelPath);

				railModel.Visible = false;

				//Generate curve and collision
				collider.Shape = new BoxShape3D()
				{
					Size = new Vector3(.15f, .3f, RailLength)
				};
				collider.Position = Vector3.Forward * RailLength * .5f;
				railPath.Curve = new Curve3D();
				railPath.Curve.AddPoint(Vector3.Zero);
				railPath.Curve.AddPoint(Vector3.Forward * RailLength);
			}
		}

		public override void _PhysicsProcess(double _)
		{
			if (Engine.IsEditorHint())
			{
				UpdateInvisibleRailLength();
				return;
			}

			if (isActive)
				UpdateRail();
			else if (isInteractingWithPlayer)
				CheckRailActivation();
		}

		private bool isCharging;
		private float chargeAmount;
		private readonly float GRIND_RAIL_CHARGE_LENGTH = .5f; //How long a full charge is.
		private readonly float GRIND_RAIL_SNAPPING = .5f; //How "magnetic" the rail is. Early 3D Sonic games tended to put this too low.
		private Vector3 closestPoint;
		private void CheckRailActivation()
		{
			if (Character.IsOnGround && !Character.JustLandedOnGround) return; //Can't start grinding from the ground
			if (Character.Lockon.IsHomingAttacking) return; //Character is targeting something
			if (Character.MovementState != CharacterController.MovementStates.Normal) return; //Character is busy

			GD.PrintErr("Grindrails may not be accurate due to PathFollower issues.");
			Vector3 delta = railPath.GlobalTransform.basis.Inverse() * (Character.GlobalPosition - railPath.GlobalPosition);
			pathFollower.Progress = railPath.Curve.GetClosestOffset(delta);
			delta = pathFollower.GetLocalPosition(Character.GlobalPosition); //Get local offset
			GD.Print(delta);

			if (Mathf.Abs(delta.x) < GRIND_RAIL_SNAPPING && Character.VerticalSpd <= 0f) //Start grinding
				ActivateRail();
		}

		private void ActivateRail()
		{
			isActive = true;
			isCharging = false;
			chargeAmount = 0;

			if (isInvisibleRail)
				railModel.Visible = true;

			Character.StartExternal(pathFollower);
			Character.MoveSpeed = Skills.grindSettings.speed;
			Character.Connect(CharacterController.SignalName.ExternalControlFinished, new Callable(this, MethodName.DisconnectFromRail), (uint)ConnectFlags.OneShot);
		}

		private void UpdateRail()
		{
			if (Character.MovementState != CharacterController.MovementStates.External) //Player must have disconnected from the rail
			{
				DisconnectFromRail();
				return;
			}

			if (Controller.jumpButton.wasPressed) //TODO jump off the rail
			{
				DisconnectFromRail();
				Character.Jump(true);
				return;
			}

			Character.MovementAngle = CharacterController.CalculateForwardAngle(pathFollower.Forward());
			Character.MoveSpeed = Skills.grindSettings.Interpolate(Character.MoveSpeed, 0f); //Slow down due to friction

			if (Controller.actionButton.wasPressed)
				isCharging = true;
			else if (isCharging)
			{
				chargeAmount = Mathf.MoveToward(chargeAmount, GRIND_RAIL_CHARGE_LENGTH, PhysicsManager.physicsDelta);

				if (Controller.actionButton.wasReleased)
				{
					float t = Mathf.SmoothStep(0, 1, chargeAmount / GRIND_RAIL_CHARGE_LENGTH);
					Character.MoveSpeed = Mathf.Lerp(Skills.unchargedGrindSpeed, Skills.chargedGrindSpeed, t);
					GD.Print($"{t} ,{Character.MoveSpeed}");

					chargeAmount = 0f;
					isCharging = false;
				}
			}

			if (isInvisibleRail)
			{
				railModel.GlobalPosition = Character.GlobalPosition;
				railMaterial.Set("uv_offset", railModel.Position.z % 1);
			}

			pathFollower.Progress += Character.MoveSpeed * PhysicsManager.physicsDelta;
			if (pathFollower.ProgressRatio >= 1) //Disconnect from the rail
				DisconnectFromRail();
		}

		private void DisconnectFromRail()
		{
			if (!isActive) return;

			isActive = false;
			isInteractingWithPlayer = false;
			Character.ResetMovementState();

			if (isInvisibleRail)
				railModel.Visible = false;

			if (Character.IsConnected(CharacterController.SignalName.ExternalControlFinished, new Callable(this, MethodName.DisconnectFromRail)))
				Character.Disconnect(CharacterController.SignalName.ExternalControlFinished, new Callable(this, MethodName.DisconnectFromRail));
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
