using Godot;
using Project.Core;

namespace Project.Interface.Menus
{
	public partial class Options : Menu
	{
		[Export]
		public ShaderMaterial menuOverlay;
		[Export]
		public SubViewport menuViewport;
		private Callable ApplyTextureCallable => new(this, MethodName.ApplyTexture);
		public static readonly StringName MENU_PARAMETER = "menu_texture";

		private Submenus currentSubmenu = Submenus.Options;
		private enum Submenus
		{
			Options, // Main menu
			Video, // Menu for configuring video settings
			Audio,  // Menu for configuring audio volume
			Language, // Menu for localization and language
			Control, // Menu for configuring input mappings
			ControlTest // Menu for testing controls
		}

		protected override void SetUp()
		{
			bgm.Play();
			if (!RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable))
				RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable);
		}

		public override void _ExitTree()
		{
			if (RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable))
				RenderingServer.Singleton.Disconnect(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable);
		}

		public void ApplyTexture()
		{
			menuOverlay.SetShaderParameter(MENU_PARAMETER, menuViewport.GetTexture());
		}



		protected override void ProcessMenu()
		{
			if (Input.IsActionJustPressed("button_pause"))
				Select();
			else
				base.ProcessMenu();
		}


		protected override void Confirm()
		{
			switch (currentSubmenu)
			{
				case Submenus.Options:
					currentSubmenu = (Submenus)VerticalSelection + 1;
					animator.Play("flip-left");
					break;
			}
		}


		protected override void Cancel()
		{
			switch (currentSubmenu)
			{
				case Submenus.Options:
					DisableProcessing();
					FadeBGM(.5f);
					menuMemory[MemoryKeys.ActiveMenu] = (int)MemoryKeys.MainMenu;
					TransitionManager.instance.Connect(TransitionManager.SignalName.TransitionProcess, new Callable(this, MethodName.TransitionFinished), (uint)ConnectFlags.OneShot);
					TransitionManager.QueueSceneChange(TransitionManager.MENU_SCENE_PATH);
					TransitionManager.StartTransition(new()
					{
						color = Colors.Black,
						inSpeed = .5f,
					});
					break;
				default:
					currentSubmenu = Submenus.Options;
					animator.Play("flip-right");
					break;
			}
		}


		private void TransitionFinished()
		{
			SaveManager.Instance.SaveConfig();
		}


		private void Select()
		{
			if (currentSubmenu == Submenus.ControlTest) // Cancel test
			{

			}
		}


		protected override void UpdateSelection()
		{
			if (Mathf.IsZeroApprox(Input.GetAxis("move_up", "move_down"))) return;

			if (currentSubmenu == Submenus.Options)
				VerticalSelection = WrapSelection(VerticalSelection + Mathf.Sign(Input.GetAxis("move_up", "move_down")), 4);

			//animator.Play("select");
			//animator.Seek(0, true);
			StartSelectionTimer();
		}


		/// <summary> Changes the visible submenu. Called from the page flip animation. </summary>
		private void UpdateSubmenuVisibility()
		{
			animator.Play(currentSubmenu.ToString().ToLower());
		}
	}
}
