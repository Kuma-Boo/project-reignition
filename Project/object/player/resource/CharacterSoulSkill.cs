using Godot;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Handles the Soul Gauge and Lockon Targets.
	/// </summary>
	public class CharacterSoulSkill : Node
	{
		public bool IsTimeBreakEnabled
		{
			get => isTimeBreakEnabled;
			set
			{
				isTimeBreakEnabled = value;
				if (IsTimeBreakActive && !isTimeBreakEnabled) //Cancel time break
					ToggleTimeBreak();
			}
		}
		public bool IsSpeedBreakEnabled
		{
			get => isSpeedBreakEnabled;
			set
			{
				isSpeedBreakEnabled = value;
				if (IsSpeedBreakActive && !isSpeedBreakEnabled) //Cancel speed break
					ToggleSpeedBreak();
			}
		}
		[Export]
		public bool isSpeedBreakEnabled;
		[Export]
		public bool isTimeBreakEnabled;

		[Export]
		public float speedBreakSpeed = 54; //Movement speed during speed break
		public bool IsTimeBreakActive { get; private set; }
		public bool IsSpeedBreakActive { get; private set; }
		public bool IsUsingBreakSkills => IsTimeBreakActive || IsSpeedBreakActive;
		
		private float breakTimer = 0; //Timer for break skills
		private const float SPEEDBREAK_DELAY = 0.32f;
		private const float BREAK_SKILLS_COOLDOWN = 1f; //Prevent skill spam
		public const float TIME_BREAK_RATIO = .6f; //Time scale

		private int soulGaugeDrainTimer;
		private const int TIME_BREAK_SOUL_DRAIN_INTERVAL = 3; //Drain 1 point every x frames

		private CharacterController Character => CharacterController.instance;

		//Cancel time break, just in case
		public override void _ExitTree() => IsTimeBreakEnabled = false;

		public void UpdateSoulSkills()
		{
			if (CheatManager.InfiniteSoulGauge)
				HeadsUpDisplay.instance.ModifySoulGauge(300);

			UpdateTimeBreak();
			UpdateSpeedBreak();

			if (!IsUsingBreakSkills)
				breakTimer = Mathf.MoveToward(breakTimer, 0, PhysicsManager.physicsDelta);
			else if (breakTimer == 0)
			{
				if (IsSpeedBreakActive)
				{
					HeadsUpDisplay.instance.ModifySoulGauge(-1);
					if (HeadsUpDisplay.instance.IsSoulGaugeEmpty)
						ToggleSpeedBreak();
				}
				else
				{
					if (soulGaugeDrainTimer == 0)
					{
						HeadsUpDisplay.instance.ModifySoulGauge(-1);
						soulGaugeDrainTimer = TIME_BREAK_SOUL_DRAIN_INTERVAL;

						if (HeadsUpDisplay.instance.IsSoulGaugeEmpty)
							ToggleTimeBreak();
					}
					soulGaugeDrainTimer--;
				}
			}
		}

		public void UpdateTimeBreak()
		{
			if (!IsTimeBreakActive && breakTimer != 0) return; //Cooldown

			if (Character.Controller.breakButton.wasPressed && !IsSpeedBreakActive)
			{
				if (IsTimeBreakActive)
				{
					ToggleTimeBreak();
					return;
				}

				if (!HeadsUpDisplay.instance.IsSoulGaugeCharged) return;

				ToggleTimeBreak();
			}
		}

		private void UpdateSpeedBreak()
		{
			if (!IsSpeedBreakActive && breakTimer != 0) return; //Cooldown

			if (Character.Controller.boostButton.wasPressed && !IsTimeBreakActive)
			{
				if (IsSpeedBreakActive) //Always allow canceling speed break
				{
					ToggleSpeedBreak();
					return;
				}

				if (!HeadsUpDisplay.instance.IsSoulGaugeCharged) return;
				if (Character.MovementState == CharacterController.MovementStates.Launcher) return;
				if (!Character.IsOnGround) return;

				ToggleSpeedBreak();
			}

			if (IsSpeedBreakActive)
			{
				if (breakTimer == 0)
				{
					if(Character.IsOnGround)
						Character.MoveSpeed = speedBreakSpeed;
				}
				else
				{
					Character.MoveSpeed = Character.StrafeSpeed = 0f;
					breakTimer = Mathf.MoveToward(breakTimer, 0, PhysicsManager.physicsDelta);
				}
			}
		}

		public void ToggleTimeBreak()
		{
			soulGaugeDrainTimer = 0;
			IsTimeBreakActive = !IsTimeBreakActive;
			Engine.TimeScale = IsTimeBreakActive ? TIME_BREAK_RATIO : 1f;

			if (IsTimeBreakActive)
			{
				Character.Sound.PlayVoice(1);
				BGMPlayer.instance.VolumeDb = -80f;
			}
			else
			{
				breakTimer = BREAK_SKILLS_COOLDOWN;
				BGMPlayer.instance.VolumeDb = 0f;
				HeadsUpDisplay.instance.UpdateSoulGaugeColor();
			}
		}

		public void ToggleSpeedBreak()
		{
			Character.ResetActionState();

			IsSpeedBreakActive = !IsSpeedBreakActive;
			breakTimer = IsSpeedBreakActive ? SPEEDBREAK_DELAY : BREAK_SKILLS_COOLDOWN;

			if (IsSpeedBreakActive)
				Character.Sound.PlayVoice(0);
			else
				Character.MoveSpeed = Character.moveSettings.speed;

			HeadsUpDisplay.instance.UpdateSoulGaugeColor();
		}
	}
}
