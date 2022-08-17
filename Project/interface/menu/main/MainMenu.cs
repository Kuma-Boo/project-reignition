using Godot;

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
				TransitionManager.Fade(Colors.Black, 2f);
				TransitionManager.instance.Connect(nameof(TransitionManager.TransitionLoad), this, nameof(Hide), null, (uint)ConnectFlags.Oneshot);
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
				TransitionManager.CompleteFade(1f);
			}

			base.Hide();
		}
	}
}
