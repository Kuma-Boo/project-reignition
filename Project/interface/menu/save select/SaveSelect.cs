using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface.Menus
{
	public partial class SaveSelect : Menu
	{
		[Export]
		private Sprite2D scrollbar;
		private Vector2 scrollbarVelocity;
		private float scrollRatio;
		private const int SCROLLBAR_HEIGHT = 276;
		private const float SCROLL_SMOOTHING = .05f;

		[Export]
		private Array<NodePath> saveOptions = new Array<NodePath>();
		private readonly Array<SaveOption> _saveOptions = new Array<SaveOption>();
		private const int ACTIVE_SAVE_OPTION_INDEX = 3; //Corresponds to the center save option

		protected override void SetUp()
		{
			VerticalSelection = menuMemory[MemoryKeys.SaveSelect];
			scrollRatio = VerticalSelection / (SaveManager.MAX_SAVE_SLOTS - 1.0f);

			for (int i = 0; i < saveOptions.Count; i++)
			{
				SaveOption option = GetNode<SaveOption>(saveOptions[i]);
				_saveOptions.Add(option);
				option.SetUp();
			}
		}

		public override void _PhysicsProcess(double _)
		{
			base._PhysicsProcess(_);

			scrollbar.Position = scrollbar.Position.SmoothDamp(Vector2.Right * SCROLLBAR_HEIGHT * scrollRatio, ref scrollbarVelocity, SCROLL_SMOOTHING);
		}

		protected override void UpdateSelection()
		{
			// Only listen for vertical scrolling
			int inputSign = Mathf.Sign(Input.GetAxis("move_up", "move_down"));
			if (inputSign == 0) return;

			VerticalSelection = WrapSelection(VerticalSelection + inputSign, SaveManager.MAX_SAVE_SLOTS);
			animator.Play(inputSign < 0 ? SCROLL_UP_ANIMATION : SCROLL_DOWN_ANIMATION);
			scrollRatio = VerticalSelection / (SaveManager.MAX_SAVE_SLOTS - 1.0f);
			menuMemory[MemoryKeys.SaveSelect] = VerticalSelection;

			if (!isSelectionScrolling)
				StartSelectionTimer();
		}

		public override void OpenSubmenu()
		{
			SaveManager.ActiveSaveSlotIndex = _saveOptions[ACTIVE_SAVE_OPTION_INDEX].SaveIndex;
			if (SaveManager.ActiveGameData.IsNewFile())
			{
				SaveManager.ResetSaveData(SaveManager.ActiveSaveSlotIndex);
				SaveManager.SaveGameToFile();

				// Load directly into the first cutscene.
				TransitionManager.QueueSceneChange($"{TransitionManager.EVENT_SCENE_PATH}1.tscn");
				TransitionManager.StartTransition(new TransitionData()
				{
					color = Colors.Black,
					inSpeed = 1f,
				});
				return;
			}

			menuMemory[MemoryKeys.WorldSelect] = (int)SaveManager.ActiveGameData.lastPlayedWorld;
			_submenus[0].ShowMenu();
		}

		/// <summary>
		/// Updates the visual data on all save options.
		/// </summary>
		public void UpdateSaveOptions()
		{
			for (int i = 0; i < _saveOptions.Count; i++)
			{
				int saveIndex = VerticalSelection + (i - ACTIVE_SAVE_OPTION_INDEX);
				saveIndex = WrapSelection(saveIndex, SaveManager.MAX_SAVE_SLOTS);
				_saveOptions[i].SaveIndex = saveIndex;
			}
		}
	}
}
