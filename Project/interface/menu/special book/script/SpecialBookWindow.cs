using Godot;

namespace Project.Interface.Menus;

public partial class SpecialBookWindow : Control
{
	[Export] private Label numberLabel;
	[Export] private AnimationPlayer windowAnimator;
	[Export] private AnimationPlayer windowAnimator2;

	public override void _Ready()
	{
		// Update the page's number
		numberLabel.Text = (GetIndex() + 1).ToString();
	}

	public void Glow() => windowAnimator2.Play("glow");
	public void Select() => windowAnimator.Play("select");
	public void Deselect() => windowAnimator.Play("RESET");
}
