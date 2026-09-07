using Godot;

namespace Project.Interface.Menus;

public partial class TimeAttackButton : Menu
{
	[Export] private string text;
	[Export] public string description { get; private set; }
	[Export] private Label label;
	[Export] public Texture2D image { get; private set; }
	[Export] private AnimationPlayer confirmAnim;

	public void SetText() => label.Text = text;
	public void ShowButton() => animator.Play("show");
	public void HideButton() => animator.Play("hide");
	public void SelectButton() => animator.Play("select");
	public void DeselectButton() => animator.Play("deselect");

	public void ConfirmButton() => confirmAnim.Play("confirm");
}
