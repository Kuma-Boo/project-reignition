using Godot;
using Godot.Collections;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface.Menus;

public partial class SkillOption : Control
{
	[Signal]
	public delegate void OnRedrawEventHandler();

	/// <summary> Reference to the target skill resource. </summary>
	public SkillResource Skill { get; set; }
	/// <summary> Is this skill a part of the augment submenu? </summary>
	public bool IsAugmentOption { get; set; }

	/// <summary> The skill option's menu number, or augment index. </summary>
	public int Number { get; set; }

	[Export]
	private Label numberLabel;
	[Export]
	private Label nameLabel;
	[Export]
	private Label costLabel;
	[Export]
	private AnimationPlayer animator;
	[Export]
	private VBoxContainer augmentContainer;
	private float augmentMenuMinimumSize;

	private SkillRing ActiveSkillRing => SaveManager.ActiveSkillRing;

	private readonly float MinimumSize = 64;
	private readonly float MinimumSizeIncrement = 63;
	/// <summary> List of all the possible augments for this skill. </summary>
	private readonly Array<SkillOption> augments = [];
	/// <summary> List of all the unlocked augments based on the current save data. </summary>
	private readonly Array<SkillOption> unlockedAugments = [];

	/// <summary> Adds a skill option to the augment list. </summary>
	public void RegisterAugment(SkillOption augment)
	{
		augmentContainer.AddChild(augment);

		if (augment.Skill.AugmentIndex == 0) // Move the base augment to the correct index (must be added last)
		{
			augments.Insert(augment.GetAugmentOffset(), augment);
			augmentContainer.MoveChild(augment, augment.GetAugmentOffset());
		}
		else
		{
			augments.Add(augment);
		}

		// Connect signal so redrawing the base skill also redraws augments
		Connect(SignalName.OnRedraw, new(augment, MethodName.Redraw));
	}

	/// <summary> Returns the number of augments available for selection. </summary>
	public int AugmentMenuCount => unlockedAugments.Count;
	/// <summary> Returns the description key of an augment. </summary>
	public StringName GetAugmentDescription(int index) => unlockedAugments[index].Skill.DescriptionKey;
	/// <summary> Returns the SkillResource of an augment. </summary>
	public SkillResource GetAugmentSkill(int index) => augments[index].Skill;

	public void UpdateUnlockedAugments()
	{
		unlockedAugments.Clear();
		augmentMenuMinimumSize = 0;

		foreach (SkillOption augment in augments)
		{
			// Update local visibility based on unlock status
			augment.Visible = SaveManager.ActiveSkillRing.IsSkillUnlocked(augment.Skill);

			if (!augment.Visible) // Skip hidden skills
				continue;

			unlockedAugments.Add(augment); // Add to unlocked augment list
			augmentMenuMinimumSize += MinimumSizeIncrement; // Update submenu size

			augment.Number = AugmentMenuCount; // Update augment number
			augment.Initialize(); // Redraw
		}

		animator.Play(AugmentMenuCount == 0 ? "disable-augment" : "enable-augment");
		animator.Advance(0);
	}

	public void ShowAugmentMenu() => animator.Play("show-augment-menu");
	public void HideAugmentMenu() => animator.Play("hide-augment-menu");

	public void Initialize()
	{
		if (Skill == null)
		{
			animator.Play("no-skill");
			return;
		}

		RedrawStaticData();
		Redraw();
	}

	public override void _Process(double _)
	{
		if (augments.Count == 0 || !augmentContainer.Visible) // Don't process when augment submenu is inactive.
			return;

		// Update minimum size based on whether the augment submenu is open or not
		float minimumSize = Mathf.Lerp(MinimumSize, MinimumSize + augmentMenuMinimumSize, augmentContainer.Scale.Y);
		CustomMinimumSize = new(CustomMinimumSize.X, minimumSize);
	}

	/// <summary> Redraws data that doesn't change when altering equip status. </summary>
	private void RedrawStaticData()
	{
		animator.Play(Skill.Element.ToString().ToLower());
		animator.Advance(0);
		animator.Play(Skill.Category.ToString().ToLower());
		animator.Advance(0);

		nameLabel.Text = Skill.NameKey;
		costLabel.Text = Skill.Cost.ToString("00");
		numberLabel.Text = Number.ToString("00");
	}

	/// <summary> Redraws data that changes when altering equip status. </summary>
	public void Redraw()
	{
		if (Skill == null)
			return;

		// Redraw equip status
		if (SaveManager.ActiveSkillRing.IsSkillEquipped(Skill.Key) &&
		SaveManager.ActiveSkillRing.GetAugmentIndex(Skill.Key) == Skill.AugmentIndex)
		{
			animator.Play("equipped");
		}
		else if (IsTooExpensive())
		{
			animator.Play("expensive");
		}
		else if (ActiveSkillRing.IsConflictingSkillEquipped(Skill.Key) != SkillKey.Count ||
			ActiveSkillRing.GetSkillCountByElement(Skill.Element) < Skill.ElementRequirement)
		{
			animator.Play("conflict");
		}
		else
		{
			animator.Play("unequipped");
		}

		animator.Advance(0);
		EmitSignal(SignalName.OnRedraw);
	}

	private bool IsTooExpensive()
	{
		int predictedCost = ActiveSkillRing.TotalCost + Skill.Cost;

		if (ActiveSkillRing.IsSkillEquipped(Skill.Key) && (Skill.IsAugment || Skill.HasAugments))
		{
			// Take augment costs into account
			int augmentIndex = ActiveSkillRing.GetAugmentIndex(Skill.Key);
			SkillResource baseSkill = Runtime.Instance.SkillList.GetSkill(Skill.Key);
			predictedCost -= baseSkill.GetAugment(augmentIndex).Cost;
		}

		return predictedCost > ActiveSkillRing.MaxSkillPoints;
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

	public int GetAugmentOffset()
	{
		int offset = 0;
		for (int i = 0; i < augments.Count; i++)
		{
			if (augments[i].Skill.AugmentIndex < 0)
				offset++;
		}

		return offset;
	}
}
