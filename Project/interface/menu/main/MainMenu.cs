using Godot;
using Godot.Collections;

namespace Project.Interface.Menus
{
	public partial class MainMenu : Menu
	{
		[Export]
		private Node2D optionParent;
		private readonly Array<Node2D> options = new Array<Node2D>();
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
		}

		protected override void SetUp()
		{
			for (int i = 0; i < optionParent.GetChildCount(); i++)
				options.Add(optionParent.GetChild<Node2D>(i));

			currentSelection = menuMemory[MemoryKeys.MainMenu];
			HorizontalSelection = currentSelection % 2;
			VerticalSelection = currentSelection / 2;
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
				animator.Play($"select");
				animator.Seek(0.0, true);
				AnimateSelection();
			}
		}

		protected override void Confirm()
		{
			//Ignore unimplemented menus.
			if (currentSelection != 0) return;
			animator.Play("confirm");
		}
		protected override void Cancel() => animator.Play("cancel");

		public override void OpenSubmenu()
		{
			if (currentSelection == 0)
				submenus[currentSelection].ShowMenu();
		}

		public void AnimateSelection() => animator.Play($"select-{currentSelection}");
	}
}
