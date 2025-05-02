using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface.Menus;

public partial class MainMenu : Menu
{
	[Export] private Description description;
	[Export] private Node2D optionParent;
	[Export] private Node2D cursor;
	private Vector2 cursorVelocity;
	private readonly Array<Node2D> options = [];
	private const float CursorSmoothing = .08f;

	private int currentSelection;

	public override void ShowMenu()
	{
		base.ShowMenu();
		cursorVelocity = Vector2.Zero;
		cursor.Position = options[currentSelection].Position;
		menuMemory[MemoryKeys.ActiveMenu] = (int)MemoryKeys.MainMenu;

		SetUp();
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

	protected override void ProcessMenu()
	{
		if (!isQuitMenuActive && Input.IsActionJustPressed("button_pause") || Input.IsActionJustPressed("ui_accept"))
		{
			quitAnimator.Play("show");
			isQuitMenuActive = true;
			isQuitSelected = false;
			return;
		}

		base.ProcessMenu();
		cursor.Position = cursor.Position.SmoothDamp(options[currentSelection].Position, ref cursorVelocity, CursorSmoothing);
	}

	[Export]
	private AnimationPlayer quitAnimator;
	private bool isQuitMenuActive;
	private bool isQuitSelected;
	private void CancelQuitMenu()
	{
		if (isQuitSelected)
		{
			quitAnimator.Play("select-no");
			quitAnimator.Advance(0.0);
		}

		isQuitMenuActive = false;
		quitAnimator.Play("hide");
	}

	protected override void UpdateSelection()
	{
		if (isQuitMenuActive)
		{
			int input = Mathf.Sign(Input.GetAxis("ui_left", "ui_right"));
			if ((input > 0 && isQuitSelected) || (input < 0 && !isQuitSelected))
			{
				isQuitSelected = !isQuitSelected;
				quitAnimator.Play(isQuitSelected ? "select-yes" : "select-no");
			}

			return;
		}

		HorizontalSelection = Mathf.Clamp(HorizontalSelection + Mathf.Sign(Input.GetAxis("ui_left", "ui_right")), 0, 1);
		VerticalSelection = Mathf.Clamp(VerticalSelection + Mathf.Sign(Input.GetAxis("ui_up", "ui_down")), 0, 1);

		int targetSelection = HorizontalSelection + (VerticalSelection * 2);
		if (targetSelection != currentSelection)
		{
			currentSelection = targetSelection;
			description.ShowDescription();
			menuMemory[MemoryKeys.MainMenu] = currentSelection;
			animator.Play("select");
			animator.Advance(0.0);
			AnimateSelection();
		}
	}

	protected override void Confirm()
	{
		if (isQuitMenuActive)
		{
			if (isQuitSelected)
				quitAnimator.Play("confirm");
			else
				CancelQuitMenu();

			return;
		}

		//Ignore unimplemented menus.
		if (currentSelection == 1) return;
		animator.Play("confirm");
	}

	protected override void Cancel()
	{
		if (isQuitMenuActive)
		{
			CancelQuitMenu();
			return;
		}

		animator.Play("cancel");
	}

	public override void OpenSubmenu()
	{
		if (currentSelection == 0)
		{
			_submenus[currentSelection].ShowMenu();
			return;
		}

		if (currentSelection < 2)
			return;

		FadeBgm(.5f);
		menuMemory[MemoryKeys.MainMenu] = currentSelection;
		TransitionManager.QueueSceneChange(currentSelection == 2 ? TransitionManager.SpecialBookScenePath : TransitionManager.OptionsScenePath);
		TransitionManager.StartTransition(new()
		{
			color = Colors.Black,
			inSpeed = .5f,
		});
	}

	private void AnimateSelection() => animator.Play($"select-{currentSelection}");

	private void StartQuitTransition()
	{
		TransitionManager.instance.Connect(TransitionManager.SignalName.TransitionProcess, new(this, MethodName.QuitGame));
		TransitionManager.StartTransition(new()
		{
			color = Colors.Black,
			inSpeed = 1f,
		});
	}
	private void QuitGame() => GetTree().Quit();
}
