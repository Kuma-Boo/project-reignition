using Godot;

namespace Project.Gameplay
{
	public partial class Bonus : Control
	{
		[Signal]
		public delegate void BonusFinishedEventHandler(Bonus b);
		public static void Queue(StringName bonus) => HeadsUpDisplay.instance.QueueBonus(bonus);

		[Export]
		private AnimationPlayer animator;
		[Export]
		private Label label;


		/// <summary> Updates the bonus text and shows the node. </summary>
		public void ShowBonus(string bonus)
		{
			label.Text = bonus;
			animator.Play("show");
		}


		/// <summary> Called after the bonus animation has finished fading out. </summary>
		private void OnBonusFinished() => EmitSignal(SignalName.BonusFinished);
	}
}
