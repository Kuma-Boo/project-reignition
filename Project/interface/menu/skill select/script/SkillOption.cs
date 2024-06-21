using Godot;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface.Menus;

public partial class SkillOption : Control
{
	public SkillKey Key { get; set; }

	public int Number { get; set; }
	public int Cost { get; set; }

	public bool IsSkillActive
	{
		get => glow.Visible;
		set => glow.Visible = value;
	}

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
		nameLabel.Text = Runtime.Instance.SkillList.GetSkill(Key).NameKey;
		costLabel.Text = Cost.ToString("00");
	}
}
