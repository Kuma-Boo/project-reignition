using Godot;
using Godot.Collections;

namespace Project.Gameplay.Hazards;

/// <summary>
/// Statemachine that progress through states based on a timer.
/// </summary>
[Tool]
public partial class TimedHazard : Hazard
{
	#region Editor
	public override Array<Dictionary> _GetPropertyList()
	{
		Array<Dictionary> properties = new()
			{
				ExtensionMethods.CreateProperty("Current State", Variant.Type.Int, PropertyHint.Enum, GetStateNames()),
				ExtensionMethods.CreateProperty("Auto Advance", Variant.Type.Bool)
			};

		if (autoAdvance)
		{
			float maxStartingTime = 0f;
			if (currentStateIndex < stateLengths.Length)
			{
				maxStartingTime = stateLengths[currentStateIndex];
				startingTime = Mathf.Clamp(startingTime, -maxStartingTime, maxStartingTime);
			}
			properties.Add(ExtensionMethods.CreateProperty("Start Time", Variant.Type.Float, PropertyHint.Range, $"{-maxStartingTime},{maxStartingTime},.1"));

			for (int i = 0; i < stateNames.Count; i++)
				properties.Add(ExtensionMethods.CreateProperty("State Lengths/" + stateNames[i], Variant.Type.Float, PropertyHint.Range, $"0,9,.1"));
		}

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
			case "Auto Advance":
				return autoAdvance;
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

		switch (propertyName)
		{
			case "Current State":
				currentStateIndex = (int)value;
				NotifyPropertyListChanged();
				break;
			case "Start Time":
				startingTime = (float)value;
				break;
			case "Auto Advance":
				autoAdvance = (bool)value;
				NotifyPropertyListChanged();
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

	/// <summary> Automatically advance to the next state when the timer is done? </summary>
	private bool autoAdvance = true;
	/// <summary> Which state to start in. </summary>
	private int currentStateIndex;
	/// <summary> What time to start with. </summary>
	private float startingTime;

	[ExportGroup("Editor")]
	/// <summary> State/Animation names. </summary>
	[Export]
	private Array<StringName> stateNames = [];
	/// <summary> How long each state should last. Set per object. </summary>
	[Export]
	private float[] stateLengths = [];

	[Export]
	private AnimationPlayer animator;
	[Export]
	private Timer timer;

	public override void _Ready()
	{
		if (Engine.IsEditorHint()) return;

		currentStateIndex = Mathf.Clamp(currentStateIndex, 0, stateNames.Count);
		if (currentStateIndex < stateLengths.Length)
			StartTimer(stateLengths[currentStateIndex] - startingTime);
		else
			OnTimerCompleted();
	}

	/// <summary> Instantly transitions to a particular state. </summary>
	public void SetState(int targetState)
	{
		if (Engine.IsEditorHint()) return;

		currentStateIndex = targetState;
		ApplyState();
	}

	private void OnTimerCompleted()
	{
		if (!autoAdvance) return;

		currentStateIndex++;
		if (currentStateIndex >= stateNames.Count)
			currentStateIndex = 0;

		ApplyState();
	}

	private void ApplyState()
	{
		double targetWaitTime = 0;
		if (currentStateIndex < stateLengths.Length)
			targetWaitTime += stateLengths[currentStateIndex];

		// Update Animation
		if (animator.HasAnimation(stateNames[currentStateIndex]))
		{
			animator.Play(stateNames[currentStateIndex]);
			animator.Seek(0, true);

			targetWaitTime += animator.CurrentAnimationLength; // Add animation length
		}

		if (autoAdvance)
			StartTimer(targetWaitTime);
	}

	private void StartTimer(double time)
	{
		if (time <= 0)
		{
			OnTimerCompleted();
		}
		else
		{
			timer.WaitTime = time;
			timer.Start();
		}
	}
}