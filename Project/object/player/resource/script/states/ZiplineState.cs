using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class ZiplineState : PlayerState
{
	public Zipline Trigger { get; set; }

	private float input;
	/// <summary> Determines whether the player's tap is registered as a double tap. </summary>
	private float doubleTapTimer;
	/// <summary> How long the player has been holding an input. </summary>
	private float inputHoldTimer;
	/// <summary> How quickly the player's input has to reach the TapDistance to count as a tap. </summary>
	private readonly float TapLength = .02f;
	/// <summary> How strong the player's input has to be to count as a tap. </summary>
	private readonly float TapRadius = .8f;
	/// <summary> How far apart taps can be to count as a double tap. </summary>
	private readonly float DoubleTapWindow = .4f;

	private bool isPromptShown;
	/// <summary> Localization key for swinging. </summary>
	private readonly string SwingAction = "action_swing";
	private float animationVelocity;
	/// <summary> How much to smooth out Sonic's zipline animation. </summary>
	private readonly float AnimationSmoothing = 2f;
	/// <summary> Timer to track of whether the player is in a damaged state. </summary>
	private float damageLockout;
	/// <summary> How long the player's inputs should be ignored after taking damage. </summary>
	private readonly float DamageLockoutLength = 0.5f;

	private int fullSwingDirection;
	/// <summary> Determines how long the player can stay in the "Tap swing" state. </summary>
	private float tapSwingTimer;
	/// <summary> How long a tap swing lasts. </summary>
	private readonly float TapSwingLength = 0.2f;
	/// <summary> How much to smooth rotations when doing a tap swing. </summary>
	private readonly float TapSwingRotationSmoothing = 10.0f;
	/// <summary> How far out the player rotates with a tap (or swing from the other side). </summary>
	private readonly float TapSwingRotationLimit = Mathf.Pi * .5f;
	/// <summary> How much to smooth rotations after a tap swing. </summary>
	private readonly float PostTapSwingRotationSmoothing = 15.0f;
	/// <summary> How much to smooth basic left and right rotations. </summary>
	private readonly float NormalRotationSmoothing = 12.0f;
	/// <summary> How far out the player can rotate without doing a full swing. </summary>
	private readonly float NormalRotationLimit = Mathf.Pi * .3f;
	/// <summary> Range where the player can start a reverse full-swing. </summary>
	private readonly float ReverseFullSwingRange = Mathf.Pi * .8f;

	public override void EnterState()
	{
		isPromptShown = false;

		damageLockout = 0;
		animationVelocity = 0;
		fullSwingDirection = 0;

		Player.Animator.StartZipline();
		Player.Animator.SetZiplineBlend(0f);
		Player.Animator.ExternalAngle = 0;
		Player.Skills.IsSpeedBreakEnabled = false;
		Player.StartExternal(Trigger, Trigger.FollowObject, .5f);

		// Update button prompt(s), but don't show them yet
		HeadsUpDisplay.Instance.SetPrompt(SwingAction, 0);
		HeadsUpDisplay.Instance.SetPrompt(null, 1);

		Player.Knockback += OnPlayerDamaged;
	}

	public override void ExitState()
	{
		Player.Skills.IsSpeedBreakEnabled = true;
		Player.StopExternal();

		HeadsUpDisplay.Instance.HidePrompts();

		Trigger.StopZipline();
		Trigger = null;

		Player.Knockback -= OnPlayerDamaged;
	}

	public override PlayerState ProcessPhysics()
	{
		damageLockout = Mathf.MoveToward(damageLockout, 0, PhysicsManager.physicsDelta);

		UpdateInputs();
		UpdateSwingButton();
		UpdatePrompts();
		UpdateAnimations();

		UpdateSwing();
		Trigger.UpdateSpeed(Trigger.ZiplineSpeed);
		Player.UpdateExternalControl(false);
		return null;
	}

	private float UpdateInputs()
	{
		if (!Mathf.IsZeroApprox(damageLockout))
		{
			input = 0;
			return input;
		}

		input = Player.Controller.InputAxis.LimitLength().X;
		if (Mathf.Abs(input) <= SaveManager.Config.deadZone) // Take dead zone into account
			input = 0;

		// Check tapping
		if (Mathf.IsZeroApprox(input))
		{
			inputHoldTimer = 0;
		}
		else if (inputHoldTimer < TapLength)
		{
			if (Mathf.Abs(input) > TapRadius)
			{
				// A tap occurred
				if (Mathf.IsZeroApprox(doubleTapTimer))
					StartTap();
				else
					StartDoubleTap();

				InvalidateTapping();
			}
			else
			{
				inputHoldTimer = Mathf.MoveToward(inputHoldTimer, 1f + TapLength, PhysicsManager.physicsDelta);
			}
		}

		doubleTapTimer = Mathf.MoveToward(doubleTapTimer, 0, PhysicsManager.physicsDelta);
		return input;
	}

	private void UpdateSwingButton()
	{
		if (!Player.Controller.IsActionBufferActive)
			return;

		Player.Controller.ResetActionBuffer();

		if (!isPromptShown)
			return;

		// Prevent player from being able to spam tap
		if (Mathf.Sign(input) != Trigger.SwingSide && Player.Animator.IsZiplineTapActive)
			return;

		if (Mathf.IsZeroApprox(doubleTapTimer))
			StartTap();
		else
			StartDoubleTap();

		InvalidateTapping();
	}

	/// <summary> Updates the hold timer so taps can't be registered until the player resets their input. </summary>
	private void InvalidateTapping() => inputHoldTimer = 1f + TapLength;

	/// <summary> Called when a tap occurs. Provides a jolt in the input direction. </summary>
	private void StartTap()
	{
		tapSwingTimer = TapSwingLength;
		doubleTapTimer = DoubleTapWindow; // Start listening for a double tap
		Player.Animator.StartZiplineTap(Mathf.Sign(input) > 0);
	}

	/// <summary> Called when a double tap occurs. Attemps a full swing. </summary>
	private void StartDoubleTap()
	{
		if (Trigger.SwingSide != Mathf.Sign(input))
		{
			// Not inputting the right direction to do a full swing; do a tap instead
			StartTap();
			return;
		}

		// A double tap occurred
		tapSwingTimer = 0;
		doubleTapTimer = 0;
		fullSwingDirection = Mathf.Sign(input);
		Player.Animator.StartZiplineTap(Mathf.Sign(input) > 0);
	}

	private void UpdatePrompts()
	{
		if (isPromptShown && Mathf.IsZeroApprox(input))
			HeadsUpDisplay.Instance.HidePrompts();
		else if (!isPromptShown && !Mathf.IsZeroApprox(input))
			HeadsUpDisplay.Instance.ShowPrompts();

		isPromptShown = !Mathf.IsZeroApprox(input);
	}

	private void UpdateAnimations()
	{
		float animationBlend = Player.Animator.GetZiplineBlend();
		animationBlend = ExtensionMethods.SmoothDamp(animationBlend, input, ref animationVelocity, AnimationSmoothing * PhysicsManager.physicsDelta);
		Player.Animator.SetZiplineBlend(animationBlend);
	}

	private void UpdateSwing()
	{
		UpdateFullSwing();
		float targetRotation = CalculateTargetRotation();
		float smoothing = CalculateRotationSmoothing();

		Trigger.UpdateRotation(targetRotation, smoothing);

		if (Mathf.Abs(Trigger.CurrentRotation) > NormalRotationLimit)
			tapSwingTimer = Mathf.MoveToward(tapSwingTimer, 0, PhysicsManager.physicsDelta);
	}

	private void UpdateFullSwing()
	{
		if (fullSwingDirection == 0) // Not performing a full swing
			return;

		if (Mathf.IsZeroApprox(input))
		{
			if (Mathf.Sign(Trigger.CurrentRotation) != fullSwingDirection)
				fullSwingDirection = 0;
			return;
		}

		float rotationMagnitude = Mathf.Abs(Trigger.CurrentRotation);
		int inputDirection = Mathf.Sign(input);
		if (inputDirection == fullSwingDirection) // Player is holding the right direction
		{
			// Prevent swings from going on forever by resetting when the player just past the reverse swing window
			if (rotationMagnitude < ReverseFullSwingRange &&
				rotationMagnitude > TapSwingRotationLimit &&
				Trigger.SwingSide == fullSwingDirection)
			{
				fullSwingDirection = 0;
			}

			return;
		}

		if (rotationMagnitude > ReverseFullSwingRange)
		{
			// Change directions
			fullSwingDirection = inputDirection;
			return;
		}

		fullSwingDirection = 0;
	}

	private float CalculateTargetRotation()
	{
		if (fullSwingDirection != 0)
			return Trigger.CurrentRotation + Mathf.Pi * .6f * fullSwingDirection;

		if (Mathf.Abs(Trigger.CurrentRotation) > TapSwingRotationLimit)
			return 0;

		if (!Mathf.IsZeroApprox(tapSwingTimer))
			return TapSwingRotationLimit * Mathf.Sign(input);

		return NormalRotationLimit * input;
	}

	private float CalculateRotationSmoothing()
	{
		// Full swings are slower at the top
		if (fullSwingDirection != 0)
			return Mathf.Abs(Trigger.CurrentRotation) > ReverseFullSwingRange ? NormalRotationSmoothing : TapSwingRotationSmoothing;

		if (!Mathf.IsZeroApprox(tapSwingTimer))
			return TapSwingRotationSmoothing;

		if (Mathf.Abs(Trigger.CurrentRotation) > NormalRotationLimit)
			return PostTapSwingRotationSmoothing;

		return NormalRotationSmoothing;
	}

	private void OnPlayerDamaged()
	{
		if (Player.IsDefeated || Player.IsInvincible) return;

		if (StageSettings.Instance.CurrentRingCount == 0)
		{
			Player.Knockback -= OnPlayerDamaged;
			Player.StartKnockback(new()
			{
				ignoreMovementState = true
			});
			return;
		}

		fullSwingDirection = 0;
		damageLockout = DamageLockoutLength;
		InvalidateTapping();

		Player.TakeDamage();
		Player.StartInvincibility();
		Player.Animator.CancelZiplineTap();
		Player.Camera.StartMediumCameraShake();

		Trigger.UpdateSpeed(0, true); // Kill speed
	}
}
