using Godot;
using Godot.Collections;

namespace Project.Gameplay.Hazards
{
	/// <summary>
	/// Statemachine that progress through states based on a timer.
	/// </summary>
	[Tool]
	public partial class TimedHazard : Hazard
	{
		#region Editor
		public override Array<Dictionary> _GetPropertyList()
		{
			Array<Dictionary> properties = new Array<Dictionary>();

			properties.Add(ExtensionMethods.CreateProperty("Current State", Variant.Type.Int, PropertyHint.Enum, GetStateNames()));

			float maxStartingTime = 0f;
			if (currentStateIndex < stateLengths.Count)
			{
				maxStartingTime = stateLengths[currentStateIndex];
				startingTime = Mathf.Clamp(startingTime, 0, maxStartingTime);
			}
			properties.Add(ExtensionMethods.CreateProperty("Start Time", Variant.Type.Float, PropertyHint.Range, $"0,{maxStartingTime},.1"));

			for (int i = 0; i < stateNames.Count; i++)
				properties.Add(ExtensionMethods.CreateProperty("State Lengths/" + stateNames[i], Variant.Type.Float, PropertyHint.Range, $"0,9,.1"));

			return properties;
		}

		public override Variant _Get(StringName property)
		{
			string propertyName = (string)property;
			if (propertyName.StartsWith("State Lengths/"))
			{
				int i = stateNames.IndexOf(propertyName.Replace("State Lengths/", string.Empty));
				if (i != -1)
					return stateLengths[i];
			}

			switch (propertyName)
			{
				case "Current State":
					return currentStateIndex;
				case "Start Time":
					return startingTime;
			}

			return base._Get(property);
		}

		public override bool _Set(StringName property, Variant value)
		{
			string propertyName = (string)property;
			if (propertyName.StartsWith("State Lengths/"))
			{
				int i = stateNames.IndexOf(propertyName.Replace("State Lengths/", string.Empty));
				if (i != -1)
				{
					stateLengths[i] = (float)value;
					return true;
				}
			}

			switch ((string)property)
			{
				case "Current State":
					currentStateIndex = (int)value;
					NotifyPropertyListChanged();
					break;
				case "Start Time":
					startingTime = (float)value;
					break;
				default:
					return false;
			}

			return true;
		}

		private string GetStateNames()
		{
			string output = "";
			for (int i = 0; i < stateNames.Count; i++)
			{
				if (i != 0)
					output += ",";

				output += stateNames[i];
			}

			return output;
		}
		#endregion

		[ExportGroup("Editor")]
		/// <summary> Which state to start in. </summary>
		[Export]
		private int currentStateIndex;
		/// <summary> What time to start with. </summary>
		[Export]
		private float startingTime;
		/// <summary> State/Animation names. </summary>
		[Export]
		private Array<StringName> stateNames = new Array<StringName>();
		/// <summary> How long each state should last. Set per object. </summary>
		[Export]
		private Array<float> stateLengths = new Array<float>();

		/*
		[Export]
		public float inactiveLength;
		[Export]
		public float warningLength;
		[Export]
		public float activeLength;
		[Export(PropertyHint.Range, "0, 1")]
		public float startingOffset;

		private AttackState attackState;
		private enum AttackState
		{
			Inactive,
			Warning,
			Active,
		}
		*/

		[Export]
		private AnimationPlayer animator;
		[Export]
		private Timer timer;

		public override void _Ready()
		{
			if (Engine.IsEditorHint()) return;

			currentStateIndex = Mathf.Clamp(currentStateIndex, 0, stateNames.Count);
			if (currentStateIndex < stateLengths.Count)
				StartTimer(stateLengths[currentStateIndex] - startingTime);
			else
				OnTimerCompleted();
		}

		public void OnTimerCompleted()
		{
			currentStateIndex++;
			if (currentStateIndex >= stateNames.Count)
				currentStateIndex = 0;

			double targetWaitTime = 0;
			if (currentStateIndex < stateLengths.Count)
				targetWaitTime += stateLengths[currentStateIndex];

			//Update Animation
			if (animator.HasAnimation(stateNames[currentStateIndex]))
			{
				animator.Play(stateNames[currentStateIndex]);
				animator.Seek(0, true);

				targetWaitTime += animator.CurrentAnimationLength; //Add animation length
			}

			StartTimer(targetWaitTime);

			/*
			switch (attackState)
			{
				case AttackState.Active:
					attackState = AttackState.Inactive;
					targetWaitTime = inactiveLength;
					animator.Play("inactive");
					break;
				case AttackState.Inactive:
					attackState = AttackState.Warning;
					targetWaitTime = warningLength;
					animator.Play("warning");
					break;
				case AttackState.Warning:
					attackState = AttackState.Active;
					targetWaitTime = activeLength;
					animator.Play("active");
					break;
			}
			*/

		}

		private void StartTimer(double time)
		{
			if (time <= 0)
				OnTimerCompleted();
			else
			{
				timer.WaitTime = time;
				timer.Start();
			}
		}
	}
}
