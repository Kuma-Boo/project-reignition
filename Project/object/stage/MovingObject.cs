using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Generic moving object. Doesn't affect rotations.
	/// </summary>
	[Tool]
	public partial class MovingObject : Node3D
	{
		#region Editor
		public override Array<Dictionary> _GetPropertyList()
		{
			Array<Dictionary> properties = new Array<Dictionary>();

			properties.Add(ExtensionMethods.CreateProperty("Movement/Mode", Variant.Type.Int, PropertyHint.Enum, movementMode.EnumToString()));
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
			Static, //Don't move object
			Linear, //Move along a line
			Circle, //Move around origin. Stretch by size
		}


		/// <summary> Current travel time. </summary>
		private float currentTime;
		/// <summary> Time scale for processing. </summary>
		public float TimeScale = 1f;
		/// <summary> Starting offset of the object. </summary>
		public float StartingOffset { get; private set; }

		/// <summary> Is movement paused? </summary>
		public bool IsPaused { get; private set; }

		[Export]
		/// <summary> Object to actually move. </summary>
		private Node3D root;
		[Export]
		/// <summary> Object to actually move. </summary>
		private AnimationPlayer animator;
		[Export(PropertyHint.Range, "0,2,.1")]
		private float animatorSpeedScale = 1.0f;

		public override void _EnterTree()
		{
			if (Engine.IsEditorHint()) return;

			if (animator != null)
				animator.SpeedScale = animatorSpeedScale;

			Reset();
		}

		public override void _PhysicsProcess(double _)
		{
			if (Engine.IsEditorHint()) return;
			if (IsMovementInvalid()) return; //No movement
			if (IsPaused) return;

			currentTime += PhysicsManager.physicsDelta * TimeScale;
			if (Mathf.Abs(currentTime) > Mathf.Abs(cycleLength)) //Rollover
				currentTime -= Mathf.Sign(cycleLength) * Mathf.Abs(cycleLength);

			if (root != null && root.IsInsideTree())
				root.GlobalPosition = InterpolatePosition(currentTime / Mathf.Abs(cycleLength));
		}

		public void Pause() => IsPaused = true;
		public void Unpause() => IsPaused = false;

		/// <summary> Resets currentTime to StartingOffset. </summary>
		public void Reset()
		{
			Unpause();
			currentTime = StartingOffset * Mathf.Abs(cycleLength);
		}

		public Vector3 InterpolatePosition(float ratio)
		{
			Vector3 targetPosition = Vector3.Zero;

			if (movementMode == MovementModes.Linear)
			{
				float linearRatio = 1f - (2 * ratio); //Convert ratio to -1 <-> 1
				ratio = Mathf.SmoothStep(0, 1, 1f - Mathf.Abs(linearRatio));

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

			return GlobalPosition + GlobalTransform.Basis * targetPosition;
		}
	}
}
