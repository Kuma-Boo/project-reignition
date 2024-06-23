using Godot;
using Godot.Collections;

namespace Project.Gameplay.Objects;

[Tool]
public partial class LaunchRing : Area3D
{
	#region Editor
	public override Array<Dictionary> _GetPropertyList()
	{
		Array<Dictionary> properties =
		[
			ExtensionMethods.CreateProperty("Editor/Spike Variant", Variant.Type.Bool),
			ExtensionMethods.CreateProperty("Settings/Distance", Variant.Type.Float),
			ExtensionMethods.CreateProperty("Settings/Middle Height", Variant.Type.Float),
			ExtensionMethods.CreateProperty("Settings/End Height", Variant.Type.Float),
		];

		if (!isSpikeVariant)
		{
			properties.Add(ExtensionMethods.CreateProperty("Settings/Min Distance", Variant.Type.Float));
			properties.Add(ExtensionMethods.CreateProperty("Settings/Min Middle Height", Variant.Type.Float));
			properties.Add(ExtensionMethods.CreateProperty("Settings/Min End Height", Variant.Type.Float));
		}

		return properties;
	}

	public override Variant _Get(StringName property)
	{
		switch ((string)property)
		{
			case "Editor/Spike Variant":
				return isSpikeVariant;

			case "Settings/Distance":
				return distance;
			case "Settings/Middle Height":
				return middleHeight;
			case "Settings/End Height":
				return endHeight;

			case "Settings/Min Distance":
				return minDistance;
			case "Settings/Min Middle Height":
				return minMiddleHeight;
			case "Settings/Min End Height":
				return minEndHeight;
		}

		return base._Get(property);
	}

	public override bool _Set(StringName property, Variant value)
	{
		switch ((string)property)
		{
			case "Editor/Spike Variant":
				isSpikeVariant = (bool)value;
				NotifyPropertyListChanged();
				break;

			case "Settings/Distance":
				distance = (float)value;
				distance = Mathf.RoundToInt(distance * 10f) * .1f;
				if (distance < 0)
					distance = 0;
				if (distance < minDistance)
					minDistance = distance;
				break;
			case "Settings/Middle Height":
				middleHeight = (float)value;
				middleHeight = Mathf.RoundToInt(middleHeight * 10f) * .1f;
				break;
			case "Settings/End Height":
				endHeight = (float)value;
				endHeight = Mathf.RoundToInt(endHeight * 10f) * .1f;
				break;

			case "Settings/Min Distance":
				minDistance = (float)value;
				minDistance = Mathf.RoundToInt(minDistance * 10f) * .1f;
				if (minDistance < 0)
					minDistance = 0;
				if (minDistance > distance)
					distance = minDistance;
				break;
			case "Settings/Min Middle Height":
				minMiddleHeight = (float)value;
				minMiddleHeight = Mathf.RoundToInt(minMiddleHeight * 10f) * .1f;
				break;
			case "Settings/Min End Height":
				minEndHeight = (float)value;
				minEndHeight = Mathf.RoundToInt(minEndHeight * 10f) * .1f;
				break;
			default:
				return false;
		}

		return true;
	}
	#endregion

	/// <summary> Is this the spike variant? </summary>
	private bool isSpikeVariant;
	[Export(PropertyHint.Range, "0, 1")]
	/// <summary> Change this in the editor to visualize launch paths. </summary>
	private float launchPower;
	public float LaunchRatio => isSpikeVariant ? 1f : Mathf.SmoothStep(0, 1, launchPower);

	//Min
	private float minDistance;
	private float minMiddleHeight;
	private float minEndHeight;

	//Max/Spike settings
	private float distance;
	private float middleHeight;
	private float endHeight;
	public LaunchSettings GetLaunchSettings()
	{
		float currentDistance = Mathf.Lerp(minDistance, distance, LaunchRatio);
		float currentMidHeight = Mathf.Lerp(minMiddleHeight, middleHeight, LaunchRatio);
		float currentEndHeight = Mathf.Lerp(minEndHeight, endHeight, LaunchRatio);
		Vector3 endPoint = GlobalPosition + (this.Forward().RemoveVertical().Normalized() * currentDistance + Vector3.Up * currentEndHeight);
		LaunchSettings data = LaunchSettings.Create(GlobalPosition, endPoint, currentMidHeight);
		data.AllowJumpDash = true;
		return data;
	}

	[ExportGroup("Editor")]
	[Export]
	private Array<NodePath> pieces;
	private readonly Array<Node3D> _pieces = new();
	private readonly int PIECE_COUNT = 16;
	private readonly float RING_SIZE = 2.2f;

	[Export]
	private AnimationPlayer animator;
	[Export]
	private AudioStreamPlayback sfx;
	private bool isActive;
	private bool isRecentered;
	private Vector3 recenterVelocity;
	private readonly float RECENTER_SPEED = .16f;

	private CharacterController Character => CharacterController.instance;

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
			//Recenter player
			Character.CenterPosition = ExtensionMethods.SmoothDamp(Character.CenterPosition, GlobalPosition, ref recenterVelocity, RECENTER_SPEED);

			if (Character.CenterPosition.DistanceSquaredTo(GlobalPosition) < .5f) //Close enough; Allow inputs
			{
				if (Input.IsActionJustPressed("button_jump")) //Disable launcher
				{
					DropPlayer();
					Character.Animator.ResetState();
					Character.Effect.StopSpinFX();
					Character.CanJumpDash = false;
				}
				else if (Input.IsActionJustPressed("button_action"))
				{
					DropPlayer();
					Character.Effect.StartTrailFX();
					Character.StartLauncher(GetLaunchSettings());
				}
			}

			Character.Animator.SetSpinSpeed(1.5f + launchPower);
		}
	}

	private void DropPlayer()
	{
		isActive = false;
		Character.ResetMovementState();
	}

	private void InitializePieces()
	{
		for (int i = 0; i < pieces.Count; i++)
			_pieces.Add(GetNode<Node3D>(pieces[i]));
	}

	private void UpdatePieces()
	{
		if (_pieces.Count == 0) return;

		float interval = Mathf.Tau / PIECE_COUNT;
		for (int i = 0; i < _pieces.Count; i++)
		{
			if (_pieces[i] == null) continue;

			Vector3 movementVector = -Vector3.Up.Rotated(Vector3.Forward, interval * (i + .5f)); //Offset rotation slightly, since visual model is offset
			_pieces[i].Position = movementVector * launchPower * RING_SIZE;
		}
	}

	private void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player")) return;

		animator.Play("charge");
		Character.StartExternal(this);
		Character.Animator.StartSpin();
		Character.Effect.StartSpinFX();

		isActive = true;
		isRecentered = false;
		recenterVelocity = Vector3.Zero;
		Character.MovementAngle = ExtensionMethods.CalculateForwardAngle(this.Forward().RemoveVertical().Normalized());
		Character.Animator.ExternalAngle = Character.MovementAngle;

			//Disable homing reticle
			Character.Lockon.IsMonitoring = false;
			Character.Lockon.StopHomingAttack();
		}

	private void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player")) return;
		animator.Play("RESET", .2 * (1 + launchPower));
	}

	public void DamagePlayer()
	{
		DropPlayer();
		Character.StartKnockback(new CharacterController.KnockbackSettings()
		{
			stayOnGround = true,
			ignoreMovementState = true,
		});
	}
}