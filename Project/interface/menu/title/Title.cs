using Godot;
using Project.Core;

namespace Project.Interface.Menu
{
	/// <summary>
	/// Press start. Also plays an intro cutscene if you wait long enough.
	/// </summary>
	public partial class Title : Menu
	{
		private bool isCutsceneActive;
		private float cutsceneTimer;
		private const float CUTSCENE_TIME_LENGTH = 5f;
		[Export]
		public NodePath animator;
		private AnimationPlayer _animator;

		protected override void SetUp()
		{
			_animator = GetNode<AnimationPlayer>(animator);
			Show();
		}

		protected override void ProcessMenu()
		{
			if (isCutsceneActive)
			{
				if (Controller.pauseButton.wasPressed || Controller.jumpButton.wasPressed)
					FinishCutscene();

				return;
			}

			cutsceneTimer += PhysicsManager.physicsDelta;
			if (cutsceneTimer >= CUTSCENE_TIME_LENGTH && !isCutsceneActive)
			{
				StartCutscene();
				return;
			}

			if (Controller.AnyButtonPressed)
			{
				//Change menu
				_animator.Play("MenuTransition");
				_submenus[0].Show(); //Activate main menu (submenu 0)
				DisableProcessing();
			}
		}

		public override void Show()
		{
			base.Show();

			_animator.Play("RESET");
			_animator.Advance(0);
			_animator.Play("TitleSpawn");

			cutsceneTimer = 0;
		}

		private void StartCutscene()
		{
			isCutsceneActive = true;
			_animator.Play("CutsceneTransition");
		}

		private void FinishCutscene()
		{
			cutsceneTimer = 0;
			isCutsceneActive = false;
			_animator.Play("RESET");
			_animator.Advance(0);
			_animator.Play("CutsceneFinishTransition");
		}
	}
}
