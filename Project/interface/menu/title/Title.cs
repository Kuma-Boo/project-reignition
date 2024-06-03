using Godot;
using Project.Core;

namespace Project.Interface.Menus
{
	/// <summary>
	/// Press start. Also plays an intro cutscene if you wait long enough.
	/// </summary>
	public partial class Title : Menu
	{
		[Export]
		private Label versionLabel;

		private bool isCutsceneActive;
		private float cutsceneTimer;
		private const float CUTSCENE_TIME_LENGTH = 20f;

		protected override void SetUp()
		{
			isProcessing = menuMemory[MemoryKeys.ActiveMenu] == (int)MemoryKeys.Title;
			versionLabel.Text = $"Version {(string)ProjectSettings.GetSetting("application/config/version")}";
			base.SetUp();
		}


		protected override void ProcessMenu()
		{
			if (isCutsceneActive)
			{
				if (Input.IsActionJustPressed("button_pause") ||
					Input.IsActionJustPressed("button_jump"))
					FinishCutscene();
			}
			else if (Input.IsAnythingPressed()) //Change menu
			{
				Confirm();
				return;
			}
			else
			{
				cutsceneTimer += PhysicsManager.physicsDelta;
				if (cutsceneTimer >= CUTSCENE_TIME_LENGTH && !isCutsceneActive)
				{
					StartCutscene();
					return;
				}
			}
		}

		//Activate main menu (submenu 0);
		public override void OpenSubmenu() => _submenus[0].ShowMenu();

		public override void ShowMenu()
		{
			animator.Play("RESET");
			animator.Seek(0, true);
			animator.Play(SHOW_ANIMATION);

			cutsceneTimer = 0;
		}

		private void StartCutscene()
		{
			isCutsceneActive = true;
			animator.Play("cutscene-start");
		}

		private void FinishCutscene()
		{
			cutsceneTimer = 0;
			isCutsceneActive = false;
			animator.Play("cutscene-finish");
		}
	}
}
