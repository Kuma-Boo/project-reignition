using Godot;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface.Menus;

public partial class LevelOption : Control
{
	[Signal]
	public delegate void NewLevelEventHandler();

	[Export]
	/// <summary> Reference to level's settings resource. </summary>
	public LevelDataResource data;

	[ExportGroup("Components")]
	[Export] private Label missionLabel;
	[Export] private Control fireSoulParent;
	[Export] private TextureRect[] fireSoulRects;
	[Export] private Texture2D fireSoulSprite;
	[Export] private Texture2D noFireSoulSprite;
	[Export] private AnimationPlayer animator;

	private readonly string NoMedalAnimation = "no-medal";
	private readonly string GoldAnimation = "gold";
	private readonly string SilverAnimation = "silver";
	private readonly string BronzeAnimation = "bronze";

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
	public SaveManager.GameData.LevelStatus ClearState { get; private set; }


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
			ClearState = SaveManager.ActiveGameData.GetClearStatus(data.LevelID);
			switch (ClearState)
			{
				case SaveManager.GameData.LevelStatus.New:
					EmitSignal(SignalName.NewLevel);
					animator.Play(NewAnimation);
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
			for (int i = 0; i < fireSoulRects.Length; i++)
			{
				bool isCollected = SaveManager.ActiveGameData.IsFireSoulCollected(data.LevelID, i + 1);
				fireSoulRects[i].Texture = isCollected ? fireSoulSprite : noFireSoulSprite;
			}
		}

		switch (SaveManager.ActiveGameData.GetRankClamped(data.LevelID))
		{
			case 1:
				animator.Play(BronzeAnimation);
				break;
			case 2:
				animator.Play(SilverAnimation);
				break;
			case 3:
				animator.Play(GoldAnimation);
				break;
			default:
				animator.Play(NoMedalAnimation);
				break;
		}
		animator.Advance(0.0);
	}
}
