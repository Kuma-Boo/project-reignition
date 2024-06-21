using Godot;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface.Menus;

public partial class LevelOption : Control
{

	[Export]
	/// <summary> Reference to level's settings resource. </summary>
	public LevelDataResource data;

	[ExportGroup("Components")]
	[Export]
	private Label missionLabel;
	[Export]
	private Node2D fireSoulParent;
	[Export]
	private Sprite2D[] fireSoulSprites;
	[Export]
	private Sprite2D rankSprite;
	[Export]
	private AnimationPlayer animator;
	private readonly string ShowAnimation = "show";
	private readonly string HideAnimation = "hide";
	private readonly string NewAnimation = "new";
	private readonly string ClearAnimation = "clear";
	private readonly string AttemptAnimation = "attempt";
	private readonly string LoopAnimation = "-loop";

	public bool IsUnlocked
	{
		get
		{
			if (string.IsNullOrEmpty(data.LevelPath)) return false; // Level doesn't exist.
			if (DebugManager.Instance.UnlockAllStages) return true;

			return SaveManager.ActiveGameData.IsStageUnlocked(data.LevelID);
		}
	}

	public string GetDescription() => IsUnlocked ? data.MissionDescriptionKey : "mission_description_locked";

	public void ShowOption()
	{
		ApplySettings();
		UpdateLevelData();
		animator.Play(ShowAnimation);
	}
	public void HideOption() => animator.Play(HideAnimation);

	private void ApplySettings()
	{
		if (missionLabel != null)
		{
			if (!Engine.IsEditorHint() && !IsUnlocked)
				missionLabel.Text = "mission_locked";
			else
				missionLabel.Text = string.IsNullOrEmpty(data.MissionTypeKey) ? "Mission Name" : data.MissionTypeKey;
		}

		if (fireSoulParent != null)
			fireSoulParent.Visible = data.HasFireSouls && (Engine.IsEditorHint() || IsUnlocked);
	}

	/// <summary> Updates level's visual data based on the player's save data. </summary>
	public void UpdateLevelData()
	{
		if (IsUnlocked)
		{
			switch (SaveManager.ActiveGameData.GetClearStatus(data.LevelID))
			{
				case SaveManager.GameData.LevelStatus.New:
					animator.Play(NewAnimation);
					animator.AnimationSetNext(ShowAnimation, NewAnimation + LoopAnimation);
					break;
				case SaveManager.GameData.LevelStatus.Attempted:
					animator.Play(AttemptAnimation);
					break;
				case SaveManager.GameData.LevelStatus.Cleared:
					animator.Play(ClearAnimation);
					animator.AnimationSetNext(ShowAnimation, ClearAnimation + LoopAnimation);
					break;
			}
		}
		else
		{
			animator.Play(AttemptAnimation); // Attempt animation also doubles as the locked animation
		}
		animator.Advance(0.0);

		if (data.HasFireSouls)
		{
			for (int i = 0; i < fireSoulSprites.Length; i++)
			{
				bool isCollected = SaveManager.ActiveGameData.IsFireSoulCollected(data.LevelID, i + 1);
				fireSoulSprites[i].RegionRect = new(new(fireSoulSprites[i].RegionRect.Position.X, isCollected ? 140 : 100), fireSoulSprites[i].RegionRect.Size);
			}
		}

		int rank = SaveManager.ActiveGameData.GetRankClamped(data.LevelID);
		rankSprite.RegionRect = new(new(96 + (40 * rank), rankSprite.RegionRect.Position.Y), rankSprite.RegionRect.Size);
	}
}