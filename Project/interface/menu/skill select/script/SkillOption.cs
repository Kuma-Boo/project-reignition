using Godot;
using Godot.Collections;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface.Menus;

public partial class SkillOption : Control
{
	public SkillResource Skill { get; set; }

	public int Number { get; set; }

	[Export]
	private Label numberLabel;
	[Export]
	private Label nameLabel;
	[Export]
	private Label costLabel;
	[Export]
	private AnimationPlayer animator;
	private SkillRing ActiveSkillRing => SaveManager.ActiveSkillRing;

	public readonly Array<SkillOption> augments = [];

	public void Initialize()
	{
		// Update all the data that doesn't change
		animator.Play(Skill.Element.ToString().ToLower());
		animator.Advance(0);
		animator.Play(Skill.Category.ToString().ToLower());
		animator.Advance(0);

		numberLabel.Text = Number.ToString("00");
		nameLabel.Text = Skill.NameKey;
		costLabel.Text = Skill.Cost.ToString("00");
		Redraw();
	}

	public void Redraw()
	{
		// Redraw equip status
		if (SaveManager.ActiveSkillRing.IsSkillEquipped(Skill.Key))
			animator.Play("equipped");
		else if (ActiveSkillRing.TotalCost + Skill.Cost > ActiveSkillRing.MaxSkillPoints)
			animator.Play("expensive");
		else if (ActiveSkillRing.IsConflictingSkillEquipped(Skill) != SkillKey.Max)
			animator.Play("conflict");
		else
			animator.Play("unequipped");

		animator.Advance(0);
	}

	public bool HasUnlockedAugments()
	{
		for (int i = 0; i < augments.Count; i++)
		{
			if (SaveManager.ActiveSkillRing.IsSkillUnlocked(augments[i].Skill))
				return true;
		}

		return false;
	}
}
