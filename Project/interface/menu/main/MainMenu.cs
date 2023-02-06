using Godot;
using Project.Core;

namespace Project.Interface.Menus
{
	public partial class MainMenu : Menu
	{
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
			else
				base.ProcessMenu();
		}
	}
}
