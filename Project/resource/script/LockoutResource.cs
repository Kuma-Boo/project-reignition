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
			properties.Add(ExtensionMethods.CreateProperty("General/Reset On Land", Variant.Type.Bool));
			properties.Add(ExtensionMethods.CreateProperty("General/Reset Actions", Variant.Type.Bool));
			properties.Add(ExtensionMethods.CreateProperty("General/Recenter Player", Variant.Type.Bool));
			properties.Add(ExtensionMethods.CreateProperty("General/Invincible", Variant.Type.Bool));
			properties.Add(ExtensionMethods.CreateProperty("General/Priority", Variant.Type.Int, PropertyHint.Range, "0, 32"));

			properties.Add(ExtensionMethods.CreateProperty("Controls/Disable Actions", Variant.Type.Bool));
			properties.Add(ExtensionMethods.CreateProperty("Controls/Override Speed", Variant.Type.Bool));
			if (overrideSpeed)
			{
				properties.Add(ExtensionMethods.CreateProperty("Controls/Speed Ratio", Variant.Type.Float, PropertyHint.Range, "0,2,.1"));
				properties.Add(ExtensionMethods.CreateProperty("Controls/Traction Multiplier", Variant.Type.Float, PropertyHint.Range, "0,4,.1"));
				properties.Add(ExtensionMethods.CreateProperty("Controls/Friction Multiplier", Variant.Type.Float, PropertyHint.Range, "0,4,.1"));
				properties.Add(ExtensionMethods.CreateProperty("Controls/Ignore Slopes", Variant.Type.Bool));
			}

			properties.Add(ExtensionMethods.CreateProperty("Controls/Movement Angle Type", Variant.Type.Int, PropertyHint.Enum, "Free,Replace,Clamp"));
			if (directionOverrideMode == DirectionOverrideMode.Replace || directionOverrideMode == DirectionOverrideMode.Clamp)
			{
				properties.Add(ExtensionMethods.CreateProperty("Controls/Allow Reversing", Variant.Type.Bool));
				properties.Add(ExtensionMethods.CreateProperty("Controls/Direction Space", Variant.Type.Int, PropertyHint.Enum, "Camera,Global,Pathfollower"));
				properties.Add(ExtensionMethods.CreateProperty("Controls/Movement Angle", Variant.Type.Float, PropertyHint.Range, "-180,180"));
				if (directionOverrideMode == DirectionOverrideMode.Clamp)
					properties.Add(ExtensionMethods.CreateProperty("Controls/Clamp Range", Variant.Type.Float, PropertyHint.Range, "0,1,.1"));
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
				case "General/Reset On Land":
					resetOnLand = (bool)value;
					break;
				case "General/Reset Actions":
					resetActions = (bool)value;
					break;
				case "General/Invincible":
					invincible = (bool)value;
					break;
				case "General/Priority":
					priority = (int)value;
					break;

				case "Controls/Disable Actions":
					disableActions = (bool)value;
					break;

				case "Controls/Override Speed":
					overrideSpeed = (bool)value;
					NotifyPropertyListChanged();
					break;
				case "Controls/Speed Ratio":
					speedRatio = (float)value;
					break;
				case "Controls/Traction Multiplier":
					tractionMultiplier = (float)value;
					break;
				case "Controls/Friction Multiplier":
					frictionMultiplier = (float)value;
					break;
				case "Controls/Ignore Slopes":
					ignoreSlopes = (bool)value;
					break;

				case "Controls/Movement Angle Type":
					directionOverrideMode = (DirectionOverrideMode)(int)value;
					NotifyPropertyListChanged();
					break;
				case "Controls/Allow Reversing":
					allowReversing = (bool)value;
					break;
				case "Controls/Direction Space":
					directionSpaceMode = (DirectionSpaceMode)(int)value;
					break;
				case "Controls/Movement Angle":
					overrideAngle = (float)value;
					break;
				case "Controls/Clamp Range":
					overrideAngleClampRange = (float)value;
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
				case "General/Reset On Land":
					return resetOnLand;
				case "General/Reset Actions":
					return resetActions;
				case "General/Invincible":
					return invincible;
				case "General/Priority":
					return priority;

				case "Controls/Disable Actions":
					return disableActions;

				case "Controls/Override Speed":
					return overrideSpeed;
				case "Controls/Speed Ratio":
					return speedRatio;
				case "Controls/Traction Multiplier":
					return tractionMultiplier;
				case "Controls/Friction Multiplier":
					return frictionMultiplier;
				case "Controls/Ignore Slopes":
					return ignoreSlopes;

				case "Controls/Movement Angle Type":
					return (int)directionOverrideMode;
				case "Controls/Allow Reversing":
					return allowReversing;
				case "Controls/Direction Space":
					return (int)directionSpaceMode;
				case "Controls/Movement Angle":
					return overrideAngle;
				case "Controls/Clamp Range":
					return overrideAngleClampRange;
			}
			return base._Get(property);
		}
		#endregion

		/// <summary> How long to remain locked out. Set this to 0 to determine with trigger nodes. </summary>
		public float length;
		/// <summary> Allows the player to regain control from landing. </summary>
		public bool resetOnLand;
		/// <summary> Resets any action the player may be doing (i.e. Sliding, Backflipping, etc)</summary>
		public bool resetActions;
		/// <summary> Lockouts with lower priorities will be unable to override higher priority lockouts </summary>
		public int priority;
		/// <summary> Collided enemies will be destroyed if this is enabled. Otherwise, the player can still take damage.</summary>
		public bool invincible;

		/// <summary> Don't let the player perform (particularly ground) actions while active </summary>
		public bool disableActions;
		/// <summary> Overriding speed? </summary>
		public bool overrideSpeed;
		/// <summary> Ratio compared to character's normal top speed. Character will move to this speed ratio </summary>
		public float speedRatio;
		/// <summary> Multiplied with character's traction. Snaps instantly when set to 0 </summary>
		public float tractionMultiplier;
		/// <summary> Multiplied with character's friction. Snaps instantly when set to 0 </summary>
		public float frictionMultiplier;
		/// <summary> Don't use slope physics when calculating speed </summary>
		public bool ignoreSlopes;
		public DirectionOverrideMode directionOverrideMode;
		public enum DirectionOverrideMode
		{
			Free, //Allows normal free movement
			Replace, //Replace movement direction with movementAngle
			Clamp, //Clamps movement direction between -movementAngleClampRange and +movementAngleClampRange
		}
		/// <summary> Allow the player to move backwards when overriding movement angle? </summary>
		public bool allowReversing;
		/// <summary> Returns the player to the center of the path </summary>
		public bool recenterPlayer;
		/// <summary> What to override movement angle to </summary>
		public float overrideAngle;
		/// <summary> Multiply with Mathf.Tau to get get clamp range in radians. </summary>
		public float overrideAngleClampRange;
		public enum DirectionSpaceMode
		{
			Camera,
			Global,
			PathFollower,
		}
		/// <summary> What "space" to calculate the movement direction in. </summary>
		public DirectionSpaceMode directionSpaceMode;

		public LockoutResource()
		{
			length = 0;
			priority = 0;
			resetOnLand = false;
			resetActions = false;
			invincible = false;

			disableActions = false;

			speedRatio = 1;
			tractionMultiplier = 1;
			frictionMultiplier = 1;
			ignoreSlopes = false;

			directionOverrideMode = DirectionOverrideMode.Free;
			directionSpaceMode = DirectionSpaceMode.Camera;
			overrideAngle = 0f;
		}

		//Compares two lockout resources based on their priority
		public class Comparer : IComparer<LockoutResource>
		{
			int IComparer<LockoutResource>.Compare(LockoutResource x, LockoutResource y) => x.priority.CompareTo(y.priority);
		}
	}
}
