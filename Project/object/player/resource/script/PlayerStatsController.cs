using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class PlayerStatsController : Node
{
	private SkillRing SkillRing => SaveManager.ActiveSkillRing;

	[ExportSubgroup("Ground Settings")]
	// Default ground settings
	[Export]
	public int baseGroundSpeed;
	[Export]
	private int baseGroundTraction;
	[Export]
	private int baseGroundFriction;
	[Export]
	private int baseGroundOverspeed;
	[Export]
	private int baseGroundTurnaround;
	[Export(PropertyHint.Range, "1,2,.1f")]
	private float groundSpeedLowRatio = 1.1f;
	[Export(PropertyHint.Range, "1,2,.1f")]
	private float groundSpeedMediumRatio = 1.3f;
	[Export(PropertyHint.Range, "1,2,.1f")]
	private float groundSpeedHighRatio = 1.6f;
	[Export(PropertyHint.Range, "1,5,.1f")]
	private float tractionLowRatio = 1.2f;
	[Export(PropertyHint.Range, "1,5,.1f")]
	private float tractionMediumRatio = 1.5f;
	[Export(PropertyHint.Range, "1,5,.1f")]
	private float tractionHighRatio = 2f;
	[Export(PropertyHint.Range, "1,5,.1f")]
	private float turnaroundHighRatio = 2f;

	public float GetBaseSpeedRatio()
	{
		if (SkillRing.IsSkillEquipped(SkillKey.SpeedUp))
		{
			switch (SkillRing.GetAugmentIndex(SkillKey.SpeedUp))
			{
				case 0:
					return groundSpeedLowRatio;
				case 1:
					return groundSpeedMediumRatio;
				case 2:
				case 3:
					return groundSpeedHighRatio;
			}
		}

		return 1.0f;
	}

	private float GetTractionRatio()
	{
		if (SkillRing.IsSkillEquipped(SkillKey.TractionUp))
		{
			switch (SkillRing.GetAugmentIndex(SkillKey.TractionUp))
			{
				case 0:
					return tractionLowRatio;
				case 1:
					return tractionMediumRatio;
				case 2:
				case 3:
					return tractionHighRatio;
			}
		}

		return 1.0f;
	}

	private float GetTurnaroundRatio() => SkillRing.IsSkillEquipped(SkillKey.TurnaroundUp) ? turnaroundHighRatio : 1.0f;

	[ExportSubgroup("Air Settings")]
	// Default air settings
	[Export]
	private int baseAirSpeed;
	[Export]
	private int baseAirTraction;
	[Export]
	private int baseAirFriction;
	[Export]
	private int baseAirOverspeed;
	[Export]
	private int baseAirTurnaround;
	[Export]
	public float accelerationJumpSpeed;

	[ExportSubgroup("Turn Settings")]
	[Export]
	private float baseMinTurn = .1f;
	[Export]
	private float baseMaxTurn = .4f;
	[Export]
	private float baseTurnTurnaround = .25f;
	[Export(PropertyHint.Range, "0,5,.1f")]
	private float quickTurnLowRatio = .9f;
	[Export(PropertyHint.Range, "0,5,.1f")]
	private float quickTurnMediumRatio = .7f;
	[Export(PropertyHint.Range, "0,5,.1f")]
	private float quickTurnHighRatio = .5f;
	[Export(PropertyHint.Range, "0,5,.1f")]
	private float slowTurnLowRatio = 1.2f;
	[Export(PropertyHint.Range, "0,5,.1f")]
	private float slowTurnMediumRatio = 1.5f;
	[Export(PropertyHint.Range, "0,5,.1f")]
	private float slowTurnHighRatio = 2f;

	/// <summary> How quickly to turn when moving slowly. </summary>
	public float MinTurnAmount { get; private set; }
	/// <summary> How quickly to turn when moving at top speed. </summary>
	public float MaxTurnAmount { get; private set; }
	/// <summary> How quickly to turn when at top speed. </summary>
	public float RecenterTurnAmount { get; private set; }
	public float GetTurnRatio()
	{
		if (SkillRing.IsSkillEquipped(SkillKey.QuickTurn))
		{
			return SkillRing.GetAugmentIndex(SkillKey.QuickTurn) switch
			{
				1 => quickTurnMediumRatio,
				2 => quickTurnHighRatio,
				_ => quickTurnLowRatio,
			};
		}

		if (SkillRing.IsSkillEquipped(SkillKey.SlowTurn))
		{
			return SkillRing.GetAugmentIndex(SkillKey.SlowTurn) switch
			{
				1 => slowTurnMediumRatio,
				2 => slowTurnHighRatio,
				_ => slowTurnLowRatio,
			};
		}

		return 1.0f;
	}

	[ExportSubgroup("Slide Settings")]
	/// <summary> Slide speed when starting from a standstill. </summary>
	[Export]
	public int InitialSlideSpeed { get; private set; }
	// Default slide settings
	[Export]
	private int baseSlideSpeed;
	[Export]
	private int baseSlideTraction;
	[Export]
	private int baseSlideFriction;
	[Export]
	private int baseSlideOverspeed;
	[Export]
	private int baseSlideTurnaround;
	[Export(PropertyHint.Range, "0,1,.1f")]
	private float slideDistanceLowFrictionRatio = .6f;
	[Export(PropertyHint.Range, "0,1,.1f")]
	private float slideDistanceMediumFrictionRatio = .4f;
	[Export(PropertyHint.Range, "0,1,.1f")]
	private float slideDistanceHighFrictionRatio = .2f;
	[Export(PropertyHint.Range, "0,1,.1f")]
	private float slideSlopeSpeedInfluence = .5f;
	/// <summary> How much should the steepest slope affect the player? </summary>
	[Export]
	public float slopeInfluence = 1f;

	/// <summary> Updates slide settings to take slope ratio into account. </summary>
	public void UpdateSlideSpeed(float slopeRatio)
	{
		// Calculate top speed
		float t = Mathf.Clamp(-slopeRatio * 5.0f, 0, 1);
		SlideSettings.Speed = GroundSettings.Speed * (1 + (t * slideSlopeSpeedInfluence));
		float slopeInfluence = slopeRatio;
		float frictionRatio = GetSlidingFrictionRatio();
		SlideSettings.Friction = baseSlideFriction * frictionRatio * (1 + slopeInfluence);
		SlideSettings.Overspeed = baseSlideOverspeed * frictionRatio * (1 + slopeInfluence);
		SlideSettings.Traction = Mathf.Lerp(0, baseSlideTraction, t);
	}

	/// <summary> Calculates sliding's friction ratio based on skills. </summary>
	public float GetSlidingFrictionRatio()
	{
		if (SkillRing.IsSkillEquipped(SkillKey.SlideDistance))
		{
			switch (SkillRing.GetAugmentIndex(SkillKey.SlideDistance))
			{
				case 0:
					return slideDistanceLowFrictionRatio;
				case 1:
					return slideDistanceMediumFrictionRatio;
				case 2:
					return slideDistanceHighFrictionRatio;
			}
		}

		return 1.0f;
	}

	// While there aren't any upgrades for the following movement skills, they're here for consistency
	[ExportGroup("Static Action Settings")]
	[Export]
	private int baseBackflipSpeed;
	[Export]
	private int baseBackflipTraction;
	[Export]
	private int baseBackflipFriction;
	[Export]
	private int baseBackflipOverspeed;
	[Export]
	private int baseBackflipTurnaround;
	[Export]
	private int baseBackstepSpeed;
	[Export]
	private int baseBackstepTraction;
	[Export]
	private int baseBackstepFriction;
	[Export]
	private int baseBackstepOverspeed;
	[Export]
	private int baseBackstepTurnaround;
	[Export]
	public float JumpHeight { get; private set; }
	[Export]
	public float DoubleJumpHeight { get; private set; }

	[ExportGroup("Grind Settings")]
	[Export]
	public float perfectShuffleSpeed;
	[Export]
	private int baseGrindSpeed;
	[Export]
	private int baseGrindFriction;
	[Export]
	private int baseGrindTurnaround;
	[Export(PropertyHint.Range, "1,2,.1f")]
	private float grindLowRatio = 1.1f;
	[Export(PropertyHint.Range, "1,2,.1f")]
	private float grindMediumRatio = 1.3f;
	[Export(PropertyHint.Range, "1,2,.1f")]
	private float grindHighRatio = 1.5f;
	public float CalculateGrindSpeedRatio()
	{
		if (SkillRing.IsSkillEquipped(SkillKey.GrindUp))
		{
			switch (SkillRing.GetAugmentIndex(SkillKey.GrindUp))
			{
				case 0:
					return grindLowRatio;
				case 1:
					return grindMediumRatio;
				case 2:
					return grindHighRatio;
			}
		}

		return 1.0f;
	}

	[ExportGroup("Sidle Settings")]
	[Export]
	public Curve sidleMovementCurve;

	// References to the actual movement settings being used
	public MovementSetting GroundSettings { get; private set; }
	public MovementSetting AirSettings { get; private set; }
	public MovementSetting BackflipSettings { get; private set; }
	public MovementSetting BackstepSettings { get; private set; }
	public MovementSetting SlideSettings { get; private set; }
	public MovementSetting GrindSettings { get; private set; }

	public void Initialize() // Create MovementSettings
	{
		// Create MovementSettings based on skills
		GroundSettings = new()
		{
			Speed = baseGroundSpeed * GetBaseSpeedRatio(),
			Traction = baseGroundTraction * GetTractionRatio(),
			Friction = baseGroundFriction,
			Overspeed = baseGroundOverspeed,
			Turnaround = baseGroundTurnaround * GetTurnaroundRatio(),
		};

		AirSettings = new()
		{
			Speed = baseAirSpeed,
			Traction = baseAirTraction,
			Friction = baseAirFriction,
			Overspeed = baseAirOverspeed,
			Turnaround = baseAirTurnaround,
		};

		BackflipSettings = new()
		{
			Speed = baseBackflipSpeed,
			Traction = baseBackflipTraction,
			Friction = baseBackflipFriction,
			Overspeed = baseBackflipOverspeed,
			Turnaround = baseBackflipTurnaround,
		};

		BackstepSettings = new()
		{
			Speed = baseBackstepSpeed,
			Traction = baseBackstepTraction,
			Friction = baseBackstepFriction,
			Overspeed = baseBackstepOverspeed,
			Turnaround = baseBackstepTurnaround,
		};

		float frictionRatio = GetSlidingFrictionRatio();
		SlideSettings = new()
		{
			Speed = baseSlideSpeed,
			Traction = baseSlideTraction,
			Friction = baseSlideFriction * frictionRatio,
			Overspeed = baseSlideOverspeed * frictionRatio,
			Turnaround = baseSlideTurnaround
		};

		GrindSettings = new()
		{
			Speed = baseGrindSpeed,
			Friction = baseGrindFriction,
			Turnaround = baseGrindTurnaround,
		};

		MinTurnAmount = baseMinTurn;
		MaxTurnAmount = baseMaxTurn * GetTurnRatio();
		RecenterTurnAmount = baseTurnTurnaround * Mathf.Min(GetTurnRatio(), 1f);
	}
}

/// <summary>
/// Contains data of movement settings. Leave values at -1 to ignore (primarily for skill overrides)
/// </summary>
public class MovementSetting
{
	public float Speed { get; set; }
	public float Traction { get; set; } // Speed up rate
	public float Friction { get; set; } // Slow down rate
	public float Overspeed { get; set; } // Slow down rate when going faster than speed
	public float Turnaround { get; set; } // Skidding

	public MovementSetting()
	{
		Speed = 0;
		Traction = 0;
		Friction = 0;
	}

	/// <summary> Interpolates between speeds based on input. </summary>
	public float UpdateInterpolate(float currentSpeed, float input)
	{
		float delta = Traction;
		float targetSpeed = Speed * input;
		targetSpeed = Mathf.Max(targetSpeed, 0);

		if (Mathf.Abs(currentSpeed) > Speed)
			delta = Overspeed;

		if (input == 0) // Deccelerate
			delta = Friction;
		else if (!Mathf.IsZeroApprox(currentSpeed) && Mathf.Sign(targetSpeed) != Mathf.Sign(Speed)) // Turnaround
			delta = Turnaround;

		return Mathf.MoveToward(currentSpeed, targetSpeed, delta * PhysicsManager.physicsDelta);
	}

	/// <summary> Special addition mode for Sliding. Does NOT support negative speeds. </summary>
	public float UpdateSlide(float currentSpeed, float input)
	{
		bool clampFinalSpeed = Mathf.Abs(currentSpeed) <= Speed;
		if (Mathf.Abs(currentSpeed) > Speed) // Reduce by overspeed
		{
			currentSpeed -= Overspeed * PhysicsManager.physicsDelta;
			if (Mathf.Abs(currentSpeed) > Speed && (Mathf.IsZeroApprox(input) || input > 0)) // Allow overspeed sliding
				return currentSpeed;
		}

		if (input > 0) // Accelerate
		{
			if (!clampFinalSpeed)
				currentSpeed = Speed;
			else
				currentSpeed += Traction * input * PhysicsManager.physicsDelta;
		}
		else
		{
			// Deccelerate and Turnaround
			currentSpeed -= Mathf.Lerp(Friction, Turnaround, Mathf.Abs(input)) * PhysicsManager.physicsDelta;
			clampFinalSpeed = Mathf.Abs(currentSpeed) <= Speed;
		}

		if (clampFinalSpeed)
			currentSpeed = Mathf.Clamp(currentSpeed, 0, Speed);

		return currentSpeed;
	}

	public float GetSpeedRatio(float spd) => spd / Speed;
	public float GetSpeedRatioClamped(float spd) => Mathf.Clamp(GetSpeedRatio(spd), -1f, 1f);
}