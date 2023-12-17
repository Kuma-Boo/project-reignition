using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface.Menus
{
	/// <summary>
	/// Base class for all menus.
	/// </summary>
	public partial class Menu : Control
	{
		public static Dictionary<MemoryKeys, int> menuMemory = new Dictionary<MemoryKeys, int>(); //Use this for determining which menu is open/which option is selected
		public enum MemoryKeys
		{
			MainMenu,
			SaveSelect,
			WorldSelect,
			LevelSelect,

			SpecialBook,

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
		public Array<Menu> submenus;

		[Export]
		protected AudioStreamPlayer bgm;
		protected float bgmFadeTime;
		[Export]
		protected AnimationPlayer animator;

		protected readonly StringName CONFIRM_ANIMATION = "confirm";
		protected readonly StringName CANCEL_ANIMATION = "cancel";
		protected readonly StringName SHOW_ANIMATION = "show";
		protected readonly StringName HIDE_ANIMATION = "hide";
		protected readonly StringName SCROLL_UP_ANIMATION = "scroll-up";
		protected readonly StringName SCROLL_DOWN_ANIMATION = "scroll-down";
		protected readonly StringName SCROLL_LEFT_ANIMATION = "scroll-up";
		protected readonly StringName SCROLL_RIGHT_ANIMATION = "scroll-down";

		protected int HorizontalSelection { get; set; }
		protected int VerticalSelection { get; set; }

		[Export]
		protected bool isProcessing; //Should we process this menu?

		public override void _Ready()
		{
			SetUp();

			if (isProcessing) //Enable isProcessing from the editor for quick single-menu debugging
				ShowMenu();
		}

		public override void _PhysicsProcess(double _)
		{
			ProcessBGMFade(); //Always process BGM fade

			if (!isProcessing || TransitionManager.IsTransitionActive) return;
			ProcessMenu();
		}

		protected virtual void SetUp() { }
		public void EnableProcessing() => isProcessing = true;
		public void DisableProcessing() => isProcessing = false;

		public virtual void ShowMenu()
		{
			// Attempt to play "show" animation
			if (animator != null && animator.HasAnimation(SHOW_ANIMATION))
				animator.Play(SHOW_ANIMATION);
			else // Fallback
				Visible = true;
		}
		public virtual void HideMenu()
		{
			// Attempt to play "hide" animation
			if (animator != null && animator.HasAnimation(HIDE_ANIMATION))
				animator.Play(HIDE_ANIMATION);
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
		protected const float SELECTION_INTERVAL = .2f;
		protected const float SELECTION_SCROLLING_INTERVAL = .1f;
		protected virtual void ProcessMenu()
		{
			if (Input.IsActionJustPressed("button_jump"))
				Confirm();
			else if (Input.IsActionJustPressed("button_action"))
				Cancel();
			else if (!Input.GetVector("move_left", "move_right", "move_up", "move_down").IsZeroApprox())
			{
				if (Mathf.IsZeroApprox(cursorSelectionTimer))
					UpdateSelection();
				else
					cursorSelectionTimer = Mathf.MoveToward(cursorSelectionTimer, 0, PhysicsManager.physicsDelta);
			}
			else
			{
				cursorSelectionTimer = 0;
				isSelectionScrolling = false;
			}
		}

		/// <summary>
		/// Called when selection was changed.
		/// </summary>
		protected virtual void UpdateSelection() { }

		/// <summary>
		/// Call this to avoid selection changing too quickly.
		/// </summary>
		protected bool isSelectionScrolling;
		protected void StartSelectionTimer()
		{
			if (!isSelectionScrolling)
			{
				isSelectionScrolling = true;
				cursorSelectionTimer = SELECTION_INTERVAL;
			}
			else
				cursorSelectionTimer = SELECTION_SCROLLING_INTERVAL;
		}

		/// <summary>
		/// Called when the Confirmbutton is pressed.
		/// </summary>
		protected virtual void Confirm()
		{
			if (animator != null && animator.HasAnimation(CONFIRM_ANIMATION))
				animator.Play(CONFIRM_ANIMATION);
		}

		/// <summary>
		/// Called when the Cancel button is pressed.
		/// </summary>
		protected virtual void Cancel()
		{
			if (animator != null && animator.HasAnimation(CANCEL_ANIMATION))
				animator.Play(CANCEL_ANIMATION);
		}

		/// <summary>
		/// Wraps a selection around max selection.
		/// </summary>
		protected int WrapSelection(int currentSelection, int maxSelection)
		{
			currentSelection %= maxSelection;
			if (currentSelection < 0)
				currentSelection += maxSelection;
			else if (currentSelection >= maxSelection)
				currentSelection -= maxSelection;

			return currentSelection;
		}

		public void PlayBGM()
		{
			if (bgm.Playing) return;


			bgmFadeTime = 0.0f; // Stops any active fading
			bgm.VolumeDb = 0.0f; // Reset volume
			bgm.Play();
		}

		/// <summary> Call this function to stop bgm instantly. </summary>
		public void StopBGM() => bgm.Stop();

		/// <summary> Call this function to start fading bgm out. </summary>
		public void FadeBGM(float fadetime) => bgmFadeTime = fadetime;

		protected void ProcessBGMFade()
		{
			if (bgm == null || !bgm.Playing || Mathf.IsZeroApprox(bgmFadeTime)) return;

			if (!SoundManager.FadeAudioPlayer(bgm, bgmFadeTime))
				bgmFadeTime = 0.0f; // Reset fade time
		}
	}
}

