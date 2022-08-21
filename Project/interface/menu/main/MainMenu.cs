using Godot;
using Project.Core;

namespace Project.Interface.Menu
{
	public class MainMenu : Menu
	{
		public static string KEY = "Main Menu"; //Menu dictionary key

		[Export]
		public NodePath animator;
		private AnimationPlayer _animator;

		protected override void SetUp()
		{
			_animator = GetNode<AnimationPlayer>(animator);
		}

		protected override void ProcessMenu()
		{
			if(Controller.actionButton.wasPressed)
			{
				TransitionManager.StartTransition(new TransitionData()
				{
					inSpeed = 2f,
					color= Colors.Black,
					type = TransitionData.Type.Fade
				});
				TransitionManager.instance.Connect(nameof(TransitionManager.PerformLoading), this, nameof(Hide), null, (uint)ConnectFlags.Oneshot);
				DisableProcessing();
			}
		}

		public override void Show()
		{
			base.Show();

			if (!memory.ContainsKey(KEY))
				memory.Add(KEY, -1);

			if(memory[KEY] != 0)
			//_animator.Play("Show");

			memory[KEY] = 0;
		}

		public override void Hide()
		{
			if (memory[KEY] == 0) //Returning to title screen
			{
				_parentMenu.Show();
				TransitionManager.FinishTransition();
			}

			base.Hide();
		}
	}
}
