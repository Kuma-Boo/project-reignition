using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface.Menus;

/// <summary>
/// Base class for all menus.
/// </summary>
public partial class Menu : Control
{
	public static Dictionary<MemoryKeys, int> menuMemory = []; //Use this for determining which menu is open/which option is selected
	public enum MemoryKeys
	{
		Title,
		MainMenu,
		SaveSelect,
		WorldSelect,
		LevelSelect,
		SkillMenuOpen,
		PresetsOpen,

		SpecialBook,

		Options,

		ActiveMenu,
		Max
	}

	public static void SetUpMemory()
	{
		for (int i = 0; i < (int)MemoryKeys.Max; i++) //Initialize all memory to 0.
			menuMemory.Add((MemoryKeys)i, 0);
	}

	[Export]
	public Menu parentMenu;
	[Export]
	public Array<NodePath> submenus;
	public Array<Menu> _submenus = []; // Also ensure the order of submenus is correct in the inspector hierarchy

	[Export]
	protected BGMPlayer bgm;
	protected float bgmFadeTime;
	[Export]
	protected AnimationPlayer animator;

	protected readonly StringName ConfirmAnimation = "confirm";
	protected readonly StringName CancelAnimation = "cancel";
	protected readonly StringName ShowAnimation = "show";
	protected readonly StringName HideAnimation = "hide";
	protected readonly StringName ScrollUpAnimation = "scroll-up";
	protected readonly StringName ScrollDownAnimation = "scroll-down";
	protected readonly StringName ScrollLeftAnimation = "scroll-up";
	protected readonly StringName ScrollRightAnimation = "scroll-down";

	protected int HorizontalSelection { get; set; }
	protected int VerticalSelection { get; set; }

	[Export]
	protected bool isProcessing; // Should we process this menu?

	public override void _Ready()
	{
		if (submenus != null)
		{
			for (int i = 0; i < submenus.Count; i++) // Required due to inspector not allowing for custom classes
			{
				Menu submenu = GetNode<Menu>(submenus[i]);
				_submenus.Add(submenu);
			}
		}

		SetUp();

		if (isProcessing) // Enable isProcessing from the editor for quick single-menu debugging
			ShowMenu();
	}

	public override void _PhysicsProcess(double _)
	{
		ProcessBgmFade(); // Always process background music fade

		if (!isProcessing || TransitionManager.IsTransitionActive) return;
		ProcessMenu();
	}

	protected virtual void SetUp() { }
	public void EnableProcessing() => isProcessing = true;
	public void DisableProcessing() => isProcessing = false;

	public virtual void ShowMenu()
	{
		// Attempt to play "show" animation
		if (animator?.HasAnimation(ShowAnimation) == true)
			animator.Play(ShowAnimation);
		else // Fallback
			Visible = true;
	}

	public virtual void HideMenu()
	{
		// Attempt to play "hide" animation
		if (animator?.HasAnimation(HideAnimation) == true)
			animator.Play(HideAnimation);
		else // Fallback
			Visible = false;
	}

	public virtual void OpenParentMenu()
	{
		if (parentMenu == null)
		{
			GD.PrintErr($"No parent menu found for '{Name}'.");
			return;
		}
		parentMenu.ShowMenu();
	}
	public virtual void OpenSubmenu() => GD.PrintErr($"Submenus unimplemented on '{Name}'.");

	/// <summary> How long between each interval selection. </summary>
	protected float cursorSelectionTimer;
	protected readonly float SelectionInterval = .2f;
	protected readonly float SelectionScrollingInterval = .1f;
	protected virtual void ProcessMenu()
	{
		if (Input.IsActionJustPressed("button_jump") || Input.IsActionJustPressed("ui_select") || Input.IsActionJustPressed("ui_select"))
		{
			Confirm();
			return;
		}

		if (Input.IsActionJustPressed("button_action") || Input.IsActionJustPressed("ui_cancel") || Input.IsActionJustPressed("ui_cancel"))
		{
			Cancel();
			return;
		}

		if (Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down").Length() > SaveManager.Config.deadZone)
		{
			if (Mathf.IsZeroApprox(cursorSelectionTimer))
				UpdateSelection();
			else
				cursorSelectionTimer = Mathf.MoveToward(cursorSelectionTimer, 0, PhysicsManager.physicsDelta);

			return;
		}

		cursorSelectionTimer = 0;
		isSelectionScrolling = false;
	}

	/// <summary> Called when selection was changed. </summary>
	protected virtual void UpdateSelection() { }

	protected bool isSelectionScrolling;
	/// <summary> Call this to avoid selection changing too quickly. </summary>
	protected void StartSelectionTimer()
	{
		if (isSelectionScrolling)
		{
			cursorSelectionTimer = SelectionScrollingInterval;
			return;
		}

		isSelectionScrolling = true;
		cursorSelectionTimer = SelectionInterval;
	}

	/// <summary> Called when the Confirmbutton is pressed. </summary>
	protected virtual void Confirm()
	{
		if (animator?.HasAnimation(ConfirmAnimation) == true)
			animator.Play(ConfirmAnimation);
	}

	/// <summary> Called when the Cancel button is pressed. </summary>
	protected virtual void Cancel()
	{
		if (animator?.HasAnimation(CancelAnimation) == true)
			animator.Play(CancelAnimation);
	}

	/// <summary> Wraps a selection around max selection. </summary>
	protected int WrapSelection(int currentSelection, int maxSelection)
	{
		currentSelection %= maxSelection;
		if (currentSelection < 0)
			currentSelection += maxSelection;
		else if (currentSelection >= maxSelection)
			currentSelection -= maxSelection;

		return currentSelection;
	}

	/// <summary> Wraps a selection around max and min selection </summary>
	protected int WrapSelection(int currentSelection, int maxSelection, int minSelection)
	{
		if (currentSelection < minSelection)
			currentSelection = maxSelection;
		else if (currentSelection > maxSelection)
			currentSelection = minSelection;

		return currentSelection;
	}

	private bool isFadingIn;
	public float CurrentBgmTime
	{
		set => bgm.Seek(value);
		get => bgm.GetPlaybackPosition() + (float)AudioServer.GetTimeSinceLastMix();
	}

	public void PlayBgm()
	{
		if (bgm == null || bgm.Playing) return;

		bgmFadeTime = 0f; // Stops any active fading
		bgm.VolumeDb = 0f; // Reset volume
		bgm.Play();
	}

	/// <summary> Call this function to stop bgm instantly. </summary>
	public void StopBgm() => bgm.Stop();

	// Overload for animation players
	public void FadeBgm(float fadetime) => FadeBgm(fadetime, false);
	/// <summary> Call this function to fade bgm in or out. </summary>
	public void FadeBgm(float fadetime, bool fadeIn, float initialVolume = 0.0f)
	{
		if (fadeIn && Mathf.IsZeroApprox(fadetime))
		{
			GD.PushWarning("Trying to fade in bgm with 0 fade time! Playing the bgm instead.");
			PlayBgm();
			return;
		}

		isFadingIn = fadeIn;
		bgmFadeTime = fadetime;

		if (isFadingIn)
		{
			bgm.VolumeDb = Mathf.LinearToDb(initialVolume);
			bgm.Play(); // Start playing
		}
	}

	protected void ProcessBgmFade()
	{
		if (isFadingIn)
		{
			if (Mathf.IsZeroApprox(bgmFadeTime))
				return;

			bgmFadeTime = Mathf.MoveToward(bgmFadeTime, 0, PhysicsManager.physicsDelta);
			bgm.VolumeDb = Mathf.MoveToward(bgm.VolumeDb, 0.0f, (80f / bgmFadeTime) * PhysicsManager.physicsDelta);
			return;
		}

		if (bgm?.Playing != true || Mathf.IsZeroApprox(bgmFadeTime)) return;

		if (!SoundManager.FadeAudioPlayer(bgm, bgmFadeTime))
			bgmFadeTime = 0.0f; // Reset fade time
	}
}