using Godot;
using Project.Core;

namespace Project.Interface.Menu
{
	public partial class MainMenu : Menu
	{
		public static string KEY = "Main Menu"; //Menu dictionary key

		[Export]
		private AnimationPlayer animator;

		protected override void ProcessMenu()
		{
			if (Controller.actionButton.wasPressed)
			{
				TransitionManager.StartTransition(new TransitionData()
				{
					inSpeed = 2f,
					color = Colors.Black,
				});
				TransitionManager.instance.Connect(TransitionManager.SignalName.TransitionProcess, new Callable(this, MethodName.Hide), (uint)ConnectFlags.OneShot);
				DisableProcessing();
			}
		}

		public override void Show()
		{
			base.Show();

			if (!memory.ContainsKey(KEY))
				memory.Add(KEY, -1);

			if (memory[KEY] != 0)
				//_animator.Play("Show");

				memory[KEY] = 0;
		}

		public override void Hide()
		{
			if (memory[KEY] == 0) //Returning to title screen
			{
				parentMenu.Show();
				TransitionManager.FinishTransition();
			}

			base.Hide();
		}
	}
}
