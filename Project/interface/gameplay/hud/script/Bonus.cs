using Godot;

namespace Project.Gameplay;

public partial class Bonus : Control
{
	[Signal]
	public delegate void BonusFinishedEventHandler(Bonus b);

	[Export]
	private AnimationPlayer animator;
	[Export]
	private Label typeLabel;
	[Export]
	private Label amountLabel;

	/// <summary> Updates the bonus text and shows the node. </summary>
	public void ShowBonus(BonusData bonus, int bonusAmount)
	{
		typeLabel.Text = bonus.Key;
		amountLabel.Text = bonusAmount > 0 ? $"+{bonusAmount}" : $"-{bonusAmount}";
		animator.Play("show");
	}

	/// <summary> Called after the bonus animation has finished fading out. </summary>
	private void OnBonusFinished() => EmitSignal(SignalName.BonusFinished);
}