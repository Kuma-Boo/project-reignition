using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay;

/// <summary>
/// Generic moving object. Doesn't affect rotations.
/// </summary>
[Tool]
public partial class MovingObject : Node3D
{
	#region Editor
	public override Array<Dictionary> _GetPropertyList()
	{
		Array<Dictionary> properties =
		[
			ExtensionMethods.CreateProperty("Movement/Mode", Variant.Type.Int, PropertyHint.Enum, movementMode.EnumToString()),
			];

		if (movementMode != MovementModes.Static)
		{
			properties.Add(ExtensionMethods.CreateProperty("Movement/Cycle Length", Variant.Type.Float, PropertyHint.Range, "-10,10,.1"));
			properties.Add(ExtensionMethods.CreateProperty("Movement/Starting Offset", Variant.Type.Float, PropertyHint.Range, "0,1,.01"));

			if (movementMode == MovementModes.Linear)
			{
				properties.Add(ExtensionMethods.CreateProperty("Movement/Distance", Variant.Type.Float, PropertyHint.Range, "0,32,.1"));
				properties.Add(ExtensionMethods.CreateProperty("Movement/Angle", Variant.Type.Float, PropertyHint.Range, "-180,180,5"));
			}
			else
			{
				properties.Add(ExtensionMethods.CreateProperty("Movement/Horizontal Size", Variant.Type.Float, PropertyHint.Range, "0,32,.1"));
				properties.Add(ExtensionMethods.CreateProperty("Movement/Vertical Size", Variant.Type.Float, PropertyHint.Range, "0,32,.1"));
				properties.Add(ExtensionMethods.CreateProperty("Movement/Radius", Variant.Type.Float, PropertyHint.Range, "0,32,.1"));
			}
		}

		properties.Add(ExtensionMethods.CreateProperty("Vertical Orientation", Variant.Type.Bool));

		return properties;
	}

	public override bool _Set(StringName property, Variant value)
	{
		switch ((string)property)
		{
			case "Movement/Mode":
				movementMode = (MovementModes)(int)value;
				NotifyPropertyListChanged();
				break;
			case "Movement/Cycle Length":
				cycleLength = (float)value;
				break;
			case "Movement/Starting Offset":
				StartingOffset = (float)value;
				break;

			case "Movement/Distance":
				distance = (float)value;
				break;
			case "Movement/Angle":
				angle = (float)value;
				break;

			case "Movement/Horizontal Size":
				size.X = (float)value;
				break;
			case "Movement/Vertical Size":
				size.Y = (float)value;
				break;
			case "Movement/Radius":
				radius = (float)value;
				break;

			case "Vertical Orientation":
				verticalOrientation = (bool)value;
				break;


			default:
				return false;
		}

		return true;
	}

	public override Variant _Get(StringName property)
	{
		switch ((string)property)
		{
			case "Movement/Mode":
				return (int)movementMode;
			case "Movement/Cycle Length":
				return cycleLength;
			case "Movement/Starting Offset":
				return StartingOffset;

			case "Movement/Distance":
				return distance;
			case "Movement/Angle":
				return angle;

			case "Movement/Horizontal Size":
				return size.X;
			case "Movement/Vertical Size":
				return size.Y;
			case "Movement/Radius":
				return radius;

			case "Vertical Orientation":
				return verticalOrientation;
		}
		return base._Get(property);
	}
	#endregion

	public bool IsMovementInvalid() => movementMode == MovementModes.Static || Mathf.IsZeroApprox(cycleLength);

	/// <summary> How far to move linearly. </summary>
	private float distance;
	/// <summary> Linear movement angle. </summary>
	private float angle;

	/// <summary> Movement radius. </summary>
	private float radius;
	/// <summary> Circular stretch amount. </summary>
	private Vector2 size;
	/// <summary> Rotates movement 90 degrees. </summary>
	private bool verticalOrientation;

	/// <summary> How long (in seconds) is a single cycle? Set negative to move object in reverse. </summary>
	private float cycleLength;

	/// <summary> How should this object move? </summary>
	public MovementModes movementMode;
	public enum MovementModes
	{
		Static, // Don't move object
		Linear, // Move along a line
		Circle, // Move around origin. Stretch by size
	}

	/// <summary> Starting offset of the object. </summary>
	public float StartingOffset { get; private set; }

	/// <summary> Processing time scale. </summary>
	public float TimeScale = 1f;
	/// <summary> Current travel time. </summary>
	private float currentTime;
	/// <summary> Set this if you want a non-linear travel time. </summary>
	[Export]
	private Curve timeCurve;
	[Export]
	private bool startPaused;
	[Export]
	private bool smoothPausing;
	/// <summary> Is movement paused? </summary>
	private bool isPaused;
	private const float PauseSmoothing = .1f;

	[Export(PropertyHint.NodePathValidTypes, "Node3D")]
	private NodePath root;
	/// <summary> Object to actually move. </summary>
	private Node3D Root;
	[Export(PropertyHint.NodePathValidTypes, "AnimationPlayer")]
	private NodePath animator;
	/// <summary> Object to actually move. </summary>
	private AnimationPlayer Animator;
	[Export(PropertyHint.Range, "0,2,.1")]
	private float animatorSpeedScale = 1.0f;
	/// <summary> Enable this if you want an animation to change speed depending on the object's movement. </summary>
	[Export]
	private bool autosetAnimatorSpeed;

	public override void _EnterTree()
	{
		Root = GetNodeOrNull<Node3D>(root);

		if (Engine.IsEditorHint())
		{
			CallDeferred(MethodName.ApplyEditorPosition);
			return;
		}

		Animator = GetNodeOrNull<AnimationPlayer>(animator);

		if (Animator != null)
			Animator.SpeedScale = animatorSpeedScale;

		StageSettings.instance.ConnectRespawnSignal(this);
		Respawn();
	}

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint()) return;
		if (IsMovementInvalid()) return; // No movement
		if (isPaused && !smoothPausing) return;

		if (smoothPausing)
			TimeScale = Mathf.Lerp(TimeScale, isPaused ? 0 : 1, PauseSmoothing);

		currentTime += PhysicsManager.physicsDelta * Mathf.Sign(cycleLength) * TimeScale;
		if (Mathf.Abs(currentTime) > Mathf.Abs(cycleLength)) // Rollover
			currentTime -= Mathf.Sign(cycleLength) * Mathf.Abs(cycleLength) * Mathf.Sign(cycleLength);

		float timeRatio = currentTime / Mathf.Abs(cycleLength);
		if (timeCurve != null) // Sample the time curve if it exists
		{
			if (timeRatio < 0) // Make sure timeRatio works as expected for negative values (i.e. sample from the end of the curve)
				timeRatio = 1 - timeRatio;
			timeRatio = timeCurve.Sample(timeRatio);
		}

		if (Root?.IsInsideTree() == true)
			Root.GlobalPosition = InterpolatePosition(timeRatio);

		if (autosetAnimatorSpeed && Animator != null) // Update animator speed as needed
			Animator.SpeedScale = animatorSpeedScale * ((timeRatio - .5f) / .5f);
	}

	public void ApplyEditorPosition()
	{
		if (Root?.IsInsideTree() != true)
			return;

		Root.GlobalPosition = InterpolatePosition(StartingOffset);
	}

	public void Pause() => isPaused = true;
	public void Unpause() => isPaused = false;

	/// <summary> Resets currentTime to StartingOffset. </summary>
	public void Respawn()
	{
		TimeScale = 1f;
		isPaused = startPaused;
		currentTime = StartingOffset * Mathf.Abs(cycleLength);

		if (Root?.IsInsideTree() == true)
			Root.GlobalPosition = InterpolatePosition(currentTime);
	}

	public Vector3 InterpolatePosition(float ratio)
	{
		Vector3 targetPosition = Vector3.Zero;

		if (movementMode == MovementModes.Linear)
		{
			if (timeCurve == null) // TODO Change default mode to Linear.
			{
				float linearRatio = 1f - (2 * ratio); // Convert ratio to -1 <-> 1
				ratio = Mathf.SmoothStep(0, 1, 1f - Mathf.Abs(linearRatio));
			}

			targetPosition = Vector3.Forward.Rotated(Vector3.Up, Mathf.DegToRad(angle));
			targetPosition *= distance * Mathf.Lerp(0, 1, ratio);
		}
		else if (movementMode == MovementModes.Circle)
		{
			Vector3 direction = Vector3.Forward.Rotated(Vector3.Up, Mathf.Tau * ratio);
			targetPosition = direction * radius;
			targetPosition.X += size.X * direction.X * .5f;
			targetPosition.Z += size.Y * direction.Z * .5f;
		}

		if (verticalOrientation)
			targetPosition = targetPosition.Rotated(Vector3.Right, Mathf.Pi * .5f);

		return GlobalPosition + (GlobalTransform.Basis * targetPosition);
	}
}