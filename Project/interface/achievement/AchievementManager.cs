using Godot;
using System.Collections.Generic;
using Project.Interface.Menus;

namespace Project.Core;

public partial class AchievementManager : Control
{
	public static AchievementManager Instance { get; private set; }

	private bool isActive;

	[Export] private SpecialBookPage[] achievements;
	private readonly Queue<int> achievementQueue = [];

	[Export] private Label title;
	[Export] private Label description;
	[Export] private AnimationPlayer animator;


	public override void _EnterTree()
	{
		Instance = this;
		animator.AnimationFinished += ((_) => isActive = false);
		SetDeferred("process_mode", (int)ProcessModeEnum.Disabled); // No need to process when no achievements are active
	}

	public override void _Process(double _)
	{
		if (isActive)
			return;

		if (achievementQueue.Count == 0) // Stop processing
		{
			ProcessMode = ProcessModeEnum.Disabled;
			return;
		}

		PlayAchievement(achievementQueue.Dequeue());
	}

	public void UnlockAchievement(StringName name)
	{
		if (SaveManager.SharedData.achievements.Contains(name)) // Already unlocked
			return;

		int index = GetAchievementIndex(name);
		if (index == -1)
		{
			GD.PushWarning($"{name.ToString()} is an invalid achievement id.");
			return;
		}

		// Update SharedData
		SaveManager.SharedData.achievements.Add(name);
		SaveManager.SaveSharedData();

		if (isActive)
		{
			achievementQueue.Enqueue(index);
			return;
		}

		PlayAchievement(index);
		ProcessMode = ProcessModeEnum.Inherit;
	}

	private void PlayAchievement(int index)
	{
		isActive = true;
		animator.Play($"medal-{achievements[index].AchievementType.ToString()}");
		animator.Advance(0.0);

		title.Text = Tr($"spb_title_{achievements[index].AchievementKey}");
		description.Text = Tr($"spb_desc_{achievements[index].AchievementKey}").Replace('!', '.').Replace("¡", string.Empty);

		animator.Play("achievement");
	}

	private int GetAchievementIndex(StringName id)
	{
		for (int i = 0; i < achievements.Length; i++)
		{
			if (achievements[i].AchievementName != id)
				continue;

			return i;
		}

		return -1;
	}
}
