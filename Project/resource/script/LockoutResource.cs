using Godot;
using Godot.Collections;
using System.Collections.Generic;

//NOTE FOR THE FUTURE. To create a custom resource in c#, create a new resource type in the file system and set the script to this c# script.
namespace Project.Gameplay
{
	[Tool]
	//Resource for lockouts. Operates on a queue system. When a lockout ends, the next highest priority will be started.
	public partial class LockoutResource : Resource
	{
		#region Editor
		public override Array<Dictionary> _GetPropertyList()
		{
			Array<Dictionary> properties = new Array<Dictionary>();

			properties.Add(ExtensionMethods.CreateProperty("General/Lockout Length", Variant.Type.Float, PropertyHint.Range, "0,20,.1"));
			properties.Add(ExtensionMethods.CreateProperty("General/Recenter Player", Variant.Type.Bool));
			properties.Add(ExtensionMethods.CreateProperty("General/Invincible", Variant.Type.Bool));
			properties.Add(ExtensionMethods.CreateProperty("General/Priority", Variant.Type.Int, PropertyHint.Range, "0, 32"));

			properties.Add(ExtensionMethods.CreateProperty("Actions/Reset Actions", Variant.Type.Bool));
			properties.Add(ExtensionMethods.CreateProperty("Actions/Disable Actions", Variant.Type.Bool));
			properties.Add(ExtensionMethods.CreateProperty("Actions/Reset Flags", Variant.Type.Int, PropertyHint.Flags, resetFlags.EnumToString()));

			properties.Add(ExtensionMethods.CreateProperty("Controls/Override Speed", Variant.Type.Bool));
			if (overrideSpeed)
			{
				properties.Add(ExtensionMethods.CreateProperty("Controls/Speed Ratio", Variant.Type.Float, PropertyHint.Range, "0,2,.1"));
				properties.Add(ExtensionMethods.CreateProperty("Controls/Traction Percentage", Variant.Type.Int, PropertyHint.Range, "-1,300,1"));
				properties.Add(ExtensionMethods.CreateProperty("Controls/Friction Percentage", Variant.Type.Int, PropertyHint.Range, "-1,300,1"));
				properties.Add(ExtensionMethods.CreateProperty("Controls/Ignore Slopes", Variant.Type.Bool));
			}

			properties.Add(ExtensionMethods.CreateProperty("Controls/Movement Type", Variant.Type.Int, PropertyHint.Enum, movementMode.EnumToString()));
			if (movementMode == MovementModes.Strafe || movementMode == MovementModes.Replace)
			{
				properties.Add(ExtensionMethods.CreateProperty("Controls/Movement Angle", Variant.Type.Float, PropertyHint.Range, "-180,180"));
				properties.Add(ExtensionMethods.CreateProperty("Controls/Direction Space", Variant.Type.Int, PropertyHint.Enum, spaceMode.EnumToString()));
				properties.Add(ExtensionMethods.CreateProperty("Controls/Allow Reversing", Variant.Type.Bool));
			}
			return properties;
		}

		public override bool _Set(StringName property, Variant value)
		{
			switch ((string)property)
			{
				case "General/Lockout Length":
					length = (float)value;
					break;
				case "General/Recenter Player":
					recenterPlayer = (bool)value;
					break;
				case "General/Invincible":
					invincible = (bool)value;
					break;
				case "General/Priority":
					priority = (int)value;
					break;

				case "Actions/Disable Actions":
					disableActions = (bool)value;
					break;
				case "Actions/Reset Flags":
					resetFlags = (ResetFlags)(int)value;
					break;

				case "Controls/Override Speed":
					overrideSpeed = (bool)value;
					NotifyPropertyListChanged();
					break;
				case "Controls/Speed Ratio":
					speedRatio = (float)value;
					break;
				case "Controls/Traction Percentage":
					tractionMultiplier = (int)value * .01f;
					break;
				case "Controls/Friction Percentage":
					frictionMultiplier = (int)value * .01f;
					break;
				case "Controls/Ignore Slopes":
					ignoreSlopes = (bool)value;
					break;

				case "Controls/Movement Type":
					movementMode = (MovementModes)(int)value;
					NotifyPropertyListChanged();
					break;
				case "Controls/Allow Reversing":
					allowReversing = (bool)value;
					break;
				case "Controls/Direction Space":
					spaceMode = (SpaceModes)(int)value;
					break;
				case "Controls/Movement Angle":
					movementAngle = (float)value;
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
				case "General/Lockout Length":
					return length;
				case "General/Recenter Player":
					return recenterPlayer;
				case "General/Invincible":
					return invincible;
				case "General/Priority":
					return priority;

				case "Actions/Disable Actions":
					return disableActions;
				case "Actions/Reset Flags":
					return (int)resetFlags;

				case "Controls/Override Speed":
					return overrideSpeed;
				case "Controls/Speed Ratio":
					return speedRatio;
				case "Controls/Traction Percentage":
					return Mathf.Round(tractionMultiplier * 100);
				case "Controls/Friction Percentage":
					return Mathf.Round(frictionMultiplier * 100);
				case "Controls/Ignore Slopes":
					return ignoreSlopes;

				case "Controls/Movement Type":
					return (int)movementMode;
				case "Controls/Allow Reversing":
					return allowReversing;
				case "Controls/Direction Space":
					return (int)spaceMode;
				case "Controls/Movement Angle":
					return movementAngle;
			}
			return base._Get(property);
		}
		#endregion

		/// <summary> How long to remain locked out. Set this to 0 to determine with trigger nodes. </summary>
		public float length;
		/// <summary> Lockouts with lower priorities will be unable to override higher priority lockouts. Priorities of -1 will be removed when overridden. </summary>
		public int priority;
		/// <summary> Collided enemies will be destroyed if this is enabled. Otherwise, the player can still take damage.</summary>
		public bool invincible;

		/// <summary> Overriding speed? </summary>
		public bool overrideSpeed;
		/// <summary> Ratio compared to character's normal top speed. Character will move to this speed ratio </summary>
		public float speedRatio;
		/// <summary> Multiplied with character's traction. </summary>
		public float tractionMultiplier;
		/// <summary> Multiplied with character's friction. </summary>
		public float frictionMultiplier;
		/// <summary> Don't use slope physics when calculating speed </summary>
		public bool ignoreSlopes;
		public MovementModes movementMode;
		public enum MovementModes
		{
			Free, //Allows free rotation-based movement
			Strafe, //Enable this to use strafing instead of rotation. Works best when DirectionSpaceMode is set to pathfollower
			Replace, //Replace movement direction with movementAngle
		}
		/// <summary> Allow the player to move backwards when overriding movement angle? </summary>
		public bool allowReversing;
		/// <summary> Returns the player to the center of the path </summary>
		public bool recenterPlayer;
		/// <summary> What to override movement angle to </summary>
		public float movementAngle;
		/// <summary> What "space" to calculate the movement direction in. </summary>
		public SpaceModes spaceMode;
		public enum SpaceModes
		{
			Camera,
			PathFollower,
			Global,
			Local,
		}

		/// <summary> Don't let the player perform (particularly ground) actions while active </summary>
		public bool disableActions;
		/// <summary> How can this lockout be reset? </summary>
		public ResetFlags resetFlags;
		public enum ResetFlags
		{
			OnJump = 1,
			OnLand = 2,
		}

		public LockoutResource()
		{
			length = 0;
			priority = 0;

			invincible = false;
			resetFlags = 0;
			disableActions = false;

			speedRatio = 1;
			tractionMultiplier = 1;
			frictionMultiplier = 1;
			ignoreSlopes = false;

			movementMode = MovementModes.Free;
			spaceMode = SpaceModes.Camera;
			movementAngle = 0f;
		}

		//Compares two lockout resources based on their priority
		public class Comparer : IComparer<LockoutResource>
		{
			int IComparer<LockoutResource>.Compare(LockoutResource x, LockoutResource y) => x.priority.CompareTo(y.priority);
		}
	}
}
