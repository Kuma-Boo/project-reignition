using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface.Menus
{
	[Tool]
	public partial class LevelOption : Control
	{
		#region Editor
		public override Array<Dictionary> _GetPropertyList()
		{
			Array<Dictionary> properties = new()
			{
				ExtensionMethods.CreateProperty("Apply Settings", Variant.Type.Bool)
			};
			return properties;
		}

		public override Variant _Get(StringName property)
		{
			switch ((string)property)
			{
				case "Apply Settings":
					return false;
			}

			return base._Get(property);
		}

		public override bool _Set(StringName property, Variant value)
		{
			switch ((string)property)
			{
				case "Apply Settings":
					ApplySettings();
					break;
				default:
					return false;
			}

			return true;
		}
		#endregion

		[Export]
		/// <summary> Path to the level's scene. </summary>
		public string levelPath;
		[Export]
		/// <summary> Level ID. Used to sync with save data. </summary>
		private string levelID;

		[Export]
		/// <summary> Should this mission be indented on the mission screen? </summary>
		public bool isSideMission;
		[Export]
		public bool hasFireSouls;
		[Export]
		public string missionNameKey;
		[Export]
		public string missionDescriptionKey;

		[ExportGroup("Components")]
		[Export]
		private Label missionLabel;
		[Export]
		private Control indentNode;
		[Export]
		private Node2D fireSoulParent;
		[Export]
		private Sprite2D[] fireSoulSprites;
		[Export]
		private Sprite2D rankSprite;
		[Export]
		private AnimationPlayer animator;

		public override void _Ready() => ApplySettings();

		public bool IsUnlocked
		{
			get
			{
				if (string.IsNullOrEmpty(levelPath)) return false; // Level doesn't exist.
				if (Core.DebugManager.Instance.UnlockAllStages) return true;

				//TODO Determine by save data
				return false;
			}
		}

		public string GetDescription() => IsUnlocked ? missionDescriptionKey : "mission_description_locked";

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
			if (indentNode != null)
				indentNode.SetAnchorAndOffset(Side.Left, 0, isSideMission ? 128 : 0);

			if (missionLabel != null)
			{
				if (!Engine.IsEditorHint() && !IsUnlocked)
					missionLabel.Text = "mission_locked";
				else
					missionLabel.Text = string.IsNullOrEmpty(missionNameKey) ? "Mission Name" : missionNameKey;
			}

			if (fireSoulParent != null)
				fireSoulParent.Visible = hasFireSouls && (Engine.IsEditorHint() || IsUnlocked);
		}


		/// <summary> Updates level's visual data based on the player's save data. </summary>
		public void UpdateLevelData()
		{
			if (string.IsNullOrEmpty(levelID)) return;

			if (hasFireSouls)
			{
				for (int i = 0; i < fireSoulSprites.Length; i++)
				{
					bool isCollected = SaveManager.ActiveGameData.IsFireSoulCollected(levelID, i + 1);
					fireSoulSprites[i].RegionRect = new(new(fireSoulSprites[i].RegionRect.Position.X, isCollected ? 140 : 100), fireSoulSprites[i].RegionRect.Size);
				}
			}

			int rank = SaveManager.ActiveGameData.GetRank(levelID);
			rankSprite.RegionRect = new(new(96 + 40 * rank, rankSprite.RegionRect.Position.Y), rankSprite.RegionRect.Size);
		}
	}
}