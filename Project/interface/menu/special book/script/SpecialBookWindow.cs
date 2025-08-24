using Godot;

namespace Project.Interface.Menus;

public partial class SpecialBookWindow : NinePatchRect
{
	[Export] private Label numberLabel;
	[Export] private AnimationPlayer windowAnimator;
	[Export] private TextureRect textureRect;
	[Export] private Label newTag;
	private Texture questionTexture;

	public void Initialize()
	{
		questionTexture = textureRect.Texture;
		numberLabel.Text = (GetIndex() + 1).ToString(); // Update the page's number
	}

	public void Select() => windowAnimator.Play("select");
	public void Deselect() => windowAnimator.Play("RESET");

	public void EnableNew() => newTag.Visible = true;
	public void DisableNew() => newTag.Visible = false;
}
