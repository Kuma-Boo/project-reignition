using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class Objectives : Control
{
	private StageSettings Stage => StageSettings.Instance;
	[ExportGroup("Objective Counter")]
	[Export]
	private Control objectiveRoot;
	[Export]
	private TextureRect objectiveSprite;
	[Export]
	private Label objectiveValue;
	[Export]
	private Label objectiveMaxValue;
	[Export]
	private AnimationPlayer[] objectiveAnimators;
	[Export]
	private AudioStreamPlayer objectiveSfx;
	public void InitializeObjectives()
	{
		objectiveRoot.Visible = Stage != null &&
			Stage.Data.MissionObjectiveCount != 0 &&
			(Stage.Data.MissionType == LevelDataResource.MissionTypes.Objective ||
			Stage.Data.MissionType == LevelDataResource.MissionTypes.Enemy ||
			Stage.Data.MissionType == LevelDataResource.MissionTypes.Chain);
		if (!objectiveRoot.Visible) return; // Don't do anything when objective counter isn't visible

		if (Stage.Data.MissionObjectiveCount != 0)
		{
			if (Stage.Data.MissionType == LevelDataResource.MissionTypes.Enemy)
				PlayObjectiveAnimation("enemy");
			else if (Stage.Data.MissionType == LevelDataResource.MissionTypes.Chain)
				PlayObjectiveAnimation("ring_chain");
		}

		objectiveValue.Text = Stage.CurrentObjectiveCount.ToString("00");
		objectiveMaxValue.Text = Stage.Data.MissionObjectiveCount.ToString("00");

		Stage.Connect(nameof(StageSettings.SignalName.ObjectiveChanged), new Callable(this, nameof(UpdateObjective)));
		Stage.Connect(nameof(StageSettings.SignalName.ObjectiveReset), new Callable(this, nameof(ResetObjective)));
	}

	public void PlayObjectiveAnimation(StringName animation) =>
		PlayObjectiveAnimation(animation, 0);


	public void PlayObjectiveAnimation(StringName animation, int index)
	{
		objectiveAnimators[index].Seek(0f);
		objectiveAnimators[index].Play(animation);
	}


	public void UpdateObjective()
	{
		if (Stage.Data.MissionType == LevelDataResource.MissionTypes.Objective ||
			Stage.Data.MissionType == LevelDataResource.MissionTypes.Enemy ||
			Stage.Data.MissionType == LevelDataResource.MissionTypes.Chain)
		{
			objectiveSfx.Play();
		}
		objectiveValue.Text = Stage.CurrentObjectiveCount.ToString("00");
	}

	private void ResetObjective() => objectiveValue.Text = Stage.CurrentObjectiveCount.ToString("00");
}
