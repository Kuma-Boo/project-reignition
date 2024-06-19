using Godot;
using Project.Gameplay;

namespace Project.Interface.Menus
{
	public partial class SkillOption : Control
	{
		public SkillKeys Key { get; set; }

		public int Number { get; set; }
		public int Cost { get; set; }

		public bool IsSkillActive
		{
			get => glow.Visible;
			set => glow.Visible = value;
		}

		public StringName DescriptionKey => NameKey + "_description";
		private StringName NameKey => "skill_" + Key.ToString().ToSnakeCase();

		[Export]
		private Sprite2D glow;
		[Export]
		private Label numberLabel;
		[Export]
		private Label nameLabel;
		[Export]
		private Label costLabel;
		[Export]
		private AnimationPlayer animator;

		public State state;
		public enum State
		{
			Locked, // Locked skill
			Equipable, // Normal
			Expensive, // Not enough SP
			Invalid, // Interferes with a different skill
		}

		public void RedrawData()
		{
			numberLabel.Text = Number.ToString("00");
			nameLabel.Text = NameKey;
			costLabel.Text = Cost.ToString("00");
		}
	}
}
