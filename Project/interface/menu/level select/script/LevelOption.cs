using Godot;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface.Menus
{
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

		public bool IsUnlocked
		{
			get
			{
				if (string.IsNullOrEmpty(data.LevelPath)) return false; // Level doesn't exist.
				if (DebugManager.Instance.UnlockAllStages) return true;

				//TODO Determine by save data
				return false;
			}
		}

		public string GetDescription() => IsUnlocked ? data.MissionDescriptionKey : "mission_description_locked";

		public void ShowOption()
		{
			animator.Play(IsUnlocked ? "unlocked" : "locked");
			animator.Advance(0);

			ApplySettings();
			UpdateLevelData();
			animator.Play("show");
		}
		public void HideOption() => animator.Play("hide");


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
			if (string.IsNullOrEmpty(data.LevelID)) return;

			if (data.HasFireSouls)
			{
				for (int i = 0; i < fireSoulSprites.Length; i++)
				{
					bool isCollected = SaveManager.ActiveGameData.IsFireSoulCollected(data.LevelID, i + 1);
					fireSoulSprites[i].RegionRect = new(new(fireSoulSprites[i].RegionRect.Position.X, isCollected ? 140 : 100), fireSoulSprites[i].RegionRect.Size);
				}
			}

			int rank = SaveManager.ActiveGameData.GetRank(data.LevelID);
			rankSprite.RegionRect = new(new(96 + 40 * rank, rankSprite.RegionRect.Position.Y), rankSprite.RegionRect.Size);
		}
	}
}