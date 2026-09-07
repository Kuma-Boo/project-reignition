using Godot;
using Project.Core;
using Project.Gameplay.Bosses;

namespace Project.Gameplay;

/// <summary> Handles the darkspine spirit bomb state. </summary>
public partial class DarkspineSpiritBombState : PlayerState
{
	[Export] private PlayerState idleState;

	public SpiritBomb SpiritBomb { get; set; }
	private float holdTimer;
	private float shakeTimer;

	/// Charging implementation is copied from DarkspineSpinState.cs
	private float slowChargeTimer;
	private bool isKickingSpiritBomb;

	/// <summary> Amount to instantly charge when the button is pressed. </summary>
	private readonly int BurstChargeAmount = 4;
	/// <summary> How quickly to charge when held down. </summary>
	private readonly float SlowChargeInterval = 0.1f;
	/// <summary> How long the spirit bomb must be pushed before it is kicked. </summary>
	private readonly float SpiritBombHoldLength = 3f;
	private readonly float ShakeTimerInterval = 0.2f;
	private readonly string ChargeAction = "action_charge";

	public override void EnterState()
	{
		Player.IsSpiritBombActive = true;

		Player.MoveSpeed = 0;
		Player.StrafeSpeed = 0;
		Player.VerticalSpeed = 0;

		isKickingSpiritBomb = false;
		holdTimer = SpiritBombHoldLength;
		shakeTimer = ShakeTimerInterval;

		Player.Camera.StartCameraShake(new PlayerCameraController.CameraShakeSettings()
		{
			fadeIn = 0,
			fadeOut = 0.1f,
			duration = 0.5f,
		});

		Player.Skills.IsTimeBreakEnabled = false;
		Player.Animator.StartSpiritBomb();
		Player.Effect.PlayDarkspineSpiritBombBurst();
		Player.StartExternal(SpiritBomb, SpiritBomb.PushPosition, 0.5f);

		slowChargeTimer = SlowChargeInterval;
		Player.Skills.ModifySoulGauge(BurstChargeAmount);

		SpiritBomb.PushCamera.Activate();
		SpiritBomb.AlfLayla.HideObjects();
		HeadsUpDisplay.Instance.SetPrompt(ChargeAction, 2);
		HeadsUpDisplay.Instance.ShowPrompts();
	}

	public override void ExitState()
	{
		Player.Skills.IsTimeBreakEnabled = true;
		Player.IsSpiritBombActive = false;
		Player.StopExternal();

		if (Player.IsKnockback)
			return;

		Player.Animator.ResetState(0.2f);
		HeadsUpDisplay.Instance.HidePrompts();
	}

	public override PlayerState ProcessPhysics()
	{
		shakeTimer = Mathf.MoveToward(shakeTimer, 0, PhysicsManager.physicsDelta);
		if (Mathf.IsZeroApprox(shakeTimer))
		{
			shakeTimer = ShakeTimerInterval;
			Player.Camera.StartCameraShake(new PlayerCameraController.CameraShakeSettings()
			{
				fadeIn = 0,
				fadeOut = 0.05f,
				duration = 0.3f,
				magnitude = Vector3.One * 0.5f,
			});
		}

		if (isKickingSpiritBomb)
		{
			if (Player.Animator.IsDarkspineKickFinished)
			{
				SpiritBomb.AlfLayla.FinishSpiritBombKick();
				return idleState;
			}

			return null;
		}

		Player.UpdateExternalControl();

		if (Player.Skills.IsSoulGaugeEmpty) // Take damage
		{
			SpiritBomb.Explode();
			return null;
		}

		holdTimer = Mathf.MoveToward(holdTimer, 0, PhysicsManager.physicsDelta);
		if (Mathf.IsZeroApprox(holdTimer))
		{
			StartSpiritBombKick();
			return null;
		}

		if (Input.IsActionJustPressed("button_attack")) // Provide more soul power when mashing
			Player.Skills.ModifySoulGauge(BurstChargeAmount);

		slowChargeTimer = Mathf.MoveToward(slowChargeTimer, 0, PhysicsManager.physicsDelta);
		if (Mathf.IsZeroApprox(slowChargeTimer))
		{
			Player.Skills.ModifySoulGauge(1);
			slowChargeTimer = SlowChargeInterval;
		}

		return null;
	}

	private void StartSpiritBombKick()
	{
		isKickingSpiritBomb = true;
		SpiritBomb.StartSpiritBombKick();
		Player.Skills.ModifySoulGauge(-Player.Skills.MaxSoulPower);
		Player.Effect.PlayVoice("ds push", -1, true);
		Player.Effect.PlayDarkspineSpiritBombBurst();
		Player.Animator.KickSpiritBomb();
		Player.Animator.SpiritBombKicked += OnSpiritBombKicked;
		HeadsUpDisplay.Instance.HidePrompts();
	}

	private void OnSpiritBombKicked()
	{
		SpiritBomb.KickSpiritBomb();
		Player.Animator.SpiritBombKicked -= OnSpiritBombKicked;
		Player.StopExternal();
	}
}
