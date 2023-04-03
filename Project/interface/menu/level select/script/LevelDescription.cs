using Godot;

namespace Project.Interface.Menus
{
	public partial class LevelDescription : Control
	{
		[Export]
		private Label descriptionLabel;
		public void SetText(string descriptionText) => descriptionLabel.Text = descriptionText;

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
