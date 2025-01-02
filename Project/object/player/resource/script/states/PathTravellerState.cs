using Godot;
using Project.Core;
using Project.Gameplay.Objects;

namespace Project.Gameplay;

public partial class PathTravellerState : PlayerState
{
	public PathTraveller Traveller { get; set; }

	private bool isRespawning;
	/// <summary> How fast is the object currently moving? </summary>
	private float currentSpeed;
	/// <summary> How much is the object currently turning? </summary>
	private Vector2 currentTurnAmount;
	// Values for smooth damp
	private float speedVelocity;
	private Vector2 turnVelocity;

	private float HorizontalTurnSmoothing => Traveller.Bounds.X - CollisionSmoothingDistance;
	private float VerticalTurnSmoothing => Traveller.Bounds.Y - CollisionSmoothingDistance;
	private readonly float SpeedSmoothing = .5f;
	private readonly float TurnSmoothing = .25f;
	/// <summary> At what distance should inputs start being smoothed? </summary>
	private readonly float CollisionSmoothingDistance = 1f;

	public override void EnterState()
	{
		Player.StartExternal(this, Traveller.PlayerStandin, .1f);
		Player.MoveSpeed = Player.VerticalSpeed = 0;
		Player.Skills.IsSpeedBreakEnabled = false;
		Player.Animator.StartBalancing(); // Carpet uses balancing animations
		Player.Animator.UpdateBalanceSpeed(1f, 0f);
		Player.Animator.ExternalAngle = 0; // Rotate to follow pathfollower
		Traveller.Staggered += OnStagger;
		Traveller.Damaged += OnDamage;
	}

	public override void ExitState()
	{
		isRespawning = false;
		currentSpeed = speedVelocity = 0;
		currentTurnAmount = turnVelocity = Vector2.Zero;

		Player.StopExternal();
		Player.Animator.ResetState();
		Player.Skills.IsSpeedBreakEnabled = true;

		Traveller.Staggered -= OnStagger;
		Traveller.Damaged -= OnDamage;
		Traveller = null;
	}

	public override PlayerState ProcessPhysics()
	{
		if (isRespawning)
			return null;

		CalculateMovement();
		if (Traveller.AutosetBounds)
			Traveller.SetHorizontalBounds(Mathf.Inf);
		UpdateCollisions(1);
		UpdateCollisions(-1);
		ApplyMovement();
		Traveller.UpdateAnimation(currentTurnAmount, currentSpeed);
		return null;
	}

	/// <summary> Handles player input. </summary>
	private void CalculateMovement()
	{
		Vector2 inputVector = Player.Controller.InputAxis * Traveller.TurnSpeed;
		if (Traveller.IsVerticalMovementDisabled) // Ignore vertical input
			inputVector.Y = 0;

		// Smooth out edges
		bool isSmoothingHorizontal = Mathf.Abs(Traveller.PathFollower.HOffset) > HorizontalTurnSmoothing &&
			Mathf.Sign(inputVector.X) != Mathf.Sign(Traveller.PathFollower.HOffset);
		bool isSmoothingVertical = Mathf.Abs(Traveller.PathFollower.VOffset) > VerticalTurnSmoothing &&
			Mathf.Sign(inputVector.Y) != Mathf.Sign(Traveller.PathFollower.VOffset);

		if (isSmoothingHorizontal)
			inputVector.X *= 1.0f - ((Mathf.Abs(Traveller.PathFollower.HOffset) - HorizontalTurnSmoothing) / (Traveller.Bounds.X - HorizontalTurnSmoothing));

		if (isSmoothingVertical)
			inputVector.Y *= 1.0f - ((Mathf.Abs(Traveller.PathFollower.VOffset) - VerticalTurnSmoothing) / (Traveller.Bounds.Y - VerticalTurnSmoothing));

		currentSpeed = ExtensionMethods.SmoothDamp(currentSpeed, Traveller.MaxSpeed, ref speedVelocity, SpeedSmoothing);
		currentTurnAmount = currentTurnAmount.SmoothDamp(inputVector, ref turnVelocity, TurnSmoothing);
	}

	/// <summary> Check for walls. </summary>
	private void UpdateCollisions(int direction)
	{
		if (!Traveller.AutosetBounds)
			return;

		float pathTravellerCollisionSize = Player.CollisionSize.X;
		float castDistance = pathTravellerCollisionSize + CollisionSmoothingDistance;
		if (Mathf.Sign(currentTurnAmount.X) == direction)
			castDistance += Mathf.Abs(currentTurnAmount.X * PhysicsManager.physicsDelta);

		Vector3 castVector = Player.Left() * direction * castDistance;
		RaycastHit wallCast = Player.CastRay(Player.GlobalPosition, castVector, Runtime.Instance.environmentMask);
		DebugManager.DrawRay(Player.GlobalPosition, castVector, wallCast ? Colors.Green : Colors.White);
		if (wallCast)
			Traveller.SetHorizontalBounds(Mathf.Abs(Traveller.PathFollower.HOffset) + (wallCast.distance - pathTravellerCollisionSize));
	}

	private void ApplyMovement()
	{
		// Update path follower
		Traveller.PathFollower.Progress += currentSpeed * PhysicsManager.physicsDelta;
		// Add offsets
		Traveller.PathFollower.HOffset -= currentTurnAmount.X * PhysicsManager.physicsDelta;
		Traveller.PathFollower.VOffset -= currentTurnAmount.Y * PhysicsManager.physicsDelta;
		// Clamp offsets
		Traveller.PathFollower.HOffset = Mathf.Clamp(Traveller.PathFollower.HOffset, -Traveller.Bounds.X, Traveller.Bounds.X);
		Traveller.PathFollower.VOffset = Mathf.Clamp(Traveller.PathFollower.VOffset, -Traveller.Bounds.Y, Traveller.Bounds.Y);

		// Sync transforms
		Traveller.GlobalTransform = Traveller.PathFollower.GlobalTransform;
		Player.UpdateExternalControl(true);
	}

	private void OnStagger()
	{
		Player.StartInvincibility();
		Player.Animator.StartBalanceStagger();
		currentSpeed = speedVelocity = 0;
		currentTurnAmount = turnVelocity = Vector2.Zero;
	}

	private void OnDamage()
	{
		Traveller.Deactivate();
		Traveller.Despawn();
		isRespawning = true;

		// Bump the player off
		LaunchSettings launchSettings = LaunchSettings.Create(Player.GlobalPosition, Player.GlobalPosition, 2);
		Player.StartLauncher(launchSettings);
		Player.Animator.ResetState(0.1f);
		Player.Animator.StartSpin(3.0f);
		Player.Animator.SnapRotation(Player.Animator.ExternalAngle);
	}
}
