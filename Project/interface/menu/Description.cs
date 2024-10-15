using Godot;

namespace Project.Interface.Menus
{
	public partial class Description : Control
	{
		[Export]
		private Label descriptionLabel;
		public string Text
		{
			get => descriptionLabel.Text;
			set => descriptionLabel.Text = value;
		}

		[Export]
		private AnimationPlayer animator;

		public void ShowDescription()
		{
			animator.Play("show");
			animator.Seek(0, true);
		}

		public void HideDescription()
		{
			animator.Play("hide");
			animator.Seek(0, true);
		}
	}
}
