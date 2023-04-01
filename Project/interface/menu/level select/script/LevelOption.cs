using Godot;
using Godot.Collections;

namespace Project.Interface.Menus
{
	[Tool]
	public partial class LevelOption : Control
	{
		#region Editor
		public override Array<Dictionary> _GetPropertyList()
		{
			Array<Dictionary> properties = new Array<Dictionary>();
			properties.Add(ExtensionMethods.CreateProperty("Apply Settings", Variant.Type.Bool));
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

		private void ApplySettings()
		{
			Control _indentNode = GetNodeOrNull<Control>(indentNode);
			if (_indentNode != null)
				_indentNode.SetAnchorAndOffset(Side.Left, 0, isSideMission ? 128 : 0);

			Label _missionLabel = GetNodeOrNull<Label>(missionLabel);
			if (_missionLabel != null)
			{
				if (!Engine.IsEditorHint() && !IsUnlocked())
					missionNameKey = "mission_locked";
				else
					_missionLabel.Text = string.IsNullOrEmpty(missionNameKey) ? "Mission Name" : missionNameKey;

			}

			Control _fireSoulNode = GetNodeOrNull<Control>(fireSoulNode);
			if (_fireSoulNode != null)
				_fireSoulNode.Visible = hasFireSouls;
		}
		#endregion

		[Export]
		/// <summary> Path to the level's scene. </summary>
		private string levelPath;
		[Export]
		/// <summary> Level ID. Used to sync with save data. </summary>
		private string levelID;

		[Export]
		/// <summary> Should this mission be indented on the mission screen? </summary>
		public bool isSideMission;
		[Export]
		private bool hasFireSouls;
		[Export]
		private string missionNameKey;
		[Export]
		private string missionDescriptionKey;

		[ExportGroup("Components")]
		[Export]
		private NodePath missionLabel;
		[Export]
		private NodePath indentNode;
		[Export]
		private NodePath fireSoulNode;
		[Export]
		private AnimationPlayer animator;

		public override void _Ready() => ApplySettings();

		public bool IsUnlocked()
		{
			if (Core.CheatManager.UnlockAllStages) return true;

			//TODO Determine by save data
			return false;
		}

		public string GetDescription() => IsUnlocked() ? missionDescriptionKey : "mission_description_locked";

		public void ShowOption() => animator.Play("show");
		public void HideOption() => animator.Play("hide");
	}
}