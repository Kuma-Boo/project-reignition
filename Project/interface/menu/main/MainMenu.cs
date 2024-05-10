using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface.Menus
{
	public partial class MainMenu : Menu
	{
		[Export]
		private Node2D optionParent;
		private readonly Array<Node2D> options = new();
		[Export]
		private Node2D cursor;
		private Vector2 cursorVelocity;
		private const float CURSOR_SMOOTHING = .08f;

		private int currentSelection;

		public override void ShowMenu()
		{
			base.ShowMenu();
			cursorVelocity = Vector2.Zero;
			cursor.Position = options[currentSelection].Position;
			menuMemory[MemoryKeys.ActiveMenu] = (int)MemoryKeys.MainMenu;
		}

		protected override void SetUp()
		{
			for (int i = 0; i < optionParent.GetChildCount(); i++)
				options.Add(optionParent.GetChild<Node2D>(i));

			currentSelection = menuMemory[MemoryKeys.MainMenu];
			HorizontalSelection = currentSelection % 2;
			VerticalSelection = currentSelection / 2;

			isProcessing = menuMemory[MemoryKeys.ActiveMenu] == (int)MemoryKeys.MainMenu;
		}

		public override void _PhysicsProcess(double _)
		{
			base._PhysicsProcess(_);

			if (IsVisibleInTree())
				cursor.Position = cursor.Position.SmoothDamp(options[currentSelection].Position, ref cursorVelocity, CURSOR_SMOOTHING);
		}

		protected override void UpdateSelection()
		{
			HorizontalSelection = Mathf.Clamp(HorizontalSelection + Mathf.Sign(Input.GetAxis("move_left", "move_right")), 0, 1);
			VerticalSelection = Mathf.Clamp(VerticalSelection + Mathf.Sign(Input.GetAxis("move_up", "move_down")), 0, 1);

			int targetSelection = HorizontalSelection + (VerticalSelection * 2);
			if (targetSelection != currentSelection)
			{
				currentSelection = targetSelection;
				menuMemory[MemoryKeys.MainMenu] = currentSelection;
				animator.Play("select");
				animator.Advance(0.0);
				AnimateSelection();
			}
		}

		protected override void Confirm()
		{
			//Ignore unimplemented menus.
			if (currentSelection == 1 || currentSelection == 2) return;
			animator.Play("confirm");
		}
		protected override void Cancel() => animator.Play("cancel");

		public override void OpenSubmenu()
		{
			if (currentSelection == 0)
				_submenus[currentSelection].ShowMenu();
			else if (currentSelection == 3)
			{
				FadeBGM(.5f);
				menuMemory[MemoryKeys.MainMenu] = currentSelection;
				TransitionManager.QueueSceneChange("res://interface/menu/options/Options.tscn");
				TransitionManager.StartTransition(new()
				{
					color = Colors.Black,
					inSpeed = .5f,
				});
			}
		}

		public void AnimateSelection() => animator.Play($"select-{currentSelection}");
	}
}
