using Godot;
using Godot.Collections;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface.Menus;

public partial class SkillPresetOption : Control
{
	public bool IsInvalid => string.IsNullOrEmpty(presetName);

	public string presetName;
	public Array<SkillKey> skills;
	public Dictionary<SkillKey, int> skillAugments;

	private SkillRing ActiveSkillRing => SaveManager.ActiveSkillRing;

	/// <summary> The preset option's menu number. </summary>
	public int DisplayNumber { get; set; }

	[Export] private Label presetLabel;

	[Export] private Label numLabel;

	[Export] private Label skillCountLabel;
	[Export] private Label skillCostLabel;

	[Export] private Label windAmtLabel;
	[Export] private Label fireAmtLabel;
	[Export] private Label darkAmtLabel;

	[Export] private AnimationPlayer animator;
	[Export] private AnimationPlayer animatorData;

	public void Initialize()
	{
		numLabel.Text = DisplayNumber.ToString("00");
		if (IsInvalid)
		{
			animator.Play("no-preset");
			return;
		}

		Redraw();
		animatorData.Play("show");
	}

	public void Redraw()
	{
		presetLabel.Text = presetName;

		int skillCount = 0;
		int skillCost = 0;
		int windAmt = 0;
		int fireAmt = 0;
		int darkAmt = 0;

		// Update all the above totals
		for (int i = 0; i < skills.Count; i++)
		{
			SkillResource baseSkill = Runtime.Instance.SkillList.GetSkill(skills[i]);
			if (baseSkill == null)
				continue;
			int augmentIndex = ActiveSkillRing.GetAugmentIndex(skills[i]);
			if (augmentIndex == 0)
			{
				switch (baseSkill.Element)
				{
					case SkillResource.SkillElement.Wind:
						windAmt++;
						break;

					case SkillResource.SkillElement.Fire:
						fireAmt++;
						break;

					case SkillResource.SkillElement.Dark:
						darkAmt++;
						break;
				}

				skillCost += baseSkill.Cost;
				skillCount++;
				continue;
			}

			switch (baseSkill.Augments[augmentIndex - 1].Element)
			{
				case SkillResource.SkillElement.Wind:
					windAmt++;
					break;

				case SkillResource.SkillElement.Fire:
					fireAmt++;
					break;

				case SkillResource.SkillElement.Dark:
					darkAmt++;
					break;
			}
			skillCost += baseSkill.Augments[augmentIndex - 1].Cost;
			skillCount++;
		}

		windAmtLabel.Text = "×" + windAmt.ToString("00");
		fireAmtLabel.Text = "×" + fireAmt.ToString("00");
		darkAmtLabel.Text = "×" + darkAmt.ToString("00");
		skillCountLabel.Text = skillCount.ToString("000");
		skillCostLabel.Text = skillCost.ToString("000");
	}

	public void Reset() => animator.Play("RESET");
	public void SelectUp() => animator.Play("select-up");

	public void SelectDown() => animator.Play("select-down");

	public void SelectLeft() => animator.Play("select-left");

	public void SelectRight() => animator.Play("select-right");

	public void SavePreset() => animator.Play("save-preset");

	public void SelectPreset() => animator.Play("load-preset");

	public void Deselect() => animator.Play("deselect");

	public void DeselectInstant() => animator.Play("deselect_instant");
}
