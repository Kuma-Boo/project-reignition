using Godot;
using Project.Gameplay;

namespace Project.Interface;

public partial class PauseSkill : Control
{
	public SkillResource Skill { get; set; }

	[Export]
	public Label nameLabel;
	[Export]
	public AnimationPlayer animator;

	public override void _Ready()
	{
		nameLabel.Text = Skill.NameKey;

		animator.Play(Skill.Element.ToString().ToLower());
		animator.Advance(0);
		animator.Play(Skill.Category.ToString().ToLower());
		animator.Advance(0);
	}
}
