using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay.Objects;

[Tool]
public partial class Ivy : Launcher
{
	[Signal] public delegate void IvyStartedEventHandler();
	private Callable ClearReversePathCallable => new(this, MethodName.ClearReversePath);

	[ExportGroup("Settings")]
	[Export(PropertyHint.Range, "2, 50")] public int Length { get; private set; }
	[Export] private float MaxRotation { get; set; }
	private float IndividualRotation => MaxRotation / Length;
	[Export] private bool ReversePathTemporarily { get; set; }

	[Export] public bool IsSleeping { get; private set; }
	[Export(PropertyHint.Range, "-1,1")]
	public float IvyRatio
	{
		get => LaunchRatio;
		private set
		{
			LaunchRatio = value;
			SetRotation();
		}
	}

	[ExportGroup("Components")]
	[Export] private PackedScene IvyScene { get; set; }
	[Export] private AudioStreamPlayer3D swingSfx;
	private Node3D linkRoot;
	private Array<Node3D> ivyLinks = [];

	private bool isInteractingWithPlayer;

	private float rotationVelocity;
	private float targetImpulse;
	private float impulseVelocity;
	private float lengthInfluence;
	private readonly float ImpulseAcceleration = 10.0f;
	private readonly float ImpulseDecceleration = 2.5f;
	private readonly float Gravity = 1.2f;
	private readonly float GravityMultiplier = 1.5f;
	private readonly float MaxRotationSpeed = 10.0f;

	protected override void SetUp()
	{
		base.SetUp();
		Initialize();

		if (Engine.IsEditorHint())
			return;

		IsSleeping = true;

		// Adjust swing speed based on length (longer ivys swing slower)
		lengthInfluence = Mathf.Clamp(50f / Length, 0.5f, 5f);
		StageSettings.Instance.Respawned += Respawn;
	}

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint())
		{
			if (Length != ivyLinks.Count)
				Initialize();
			SetRotation();
			return;
		}

		if (IsSleeping)
			return;

		UpdateSwing();
		CallDeferred(MethodName.UpdateAreaPosition);
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection") || Engine.IsEditorHint())
			return;

		if (Player.IsLaunching && Player.ActiveLauncher == this)
			return;

		Player.StartIvy(this);
		isInteractingWithPlayer = true;
		EmitSignal(SignalName.IvyStarted);
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player detection") || Engine.IsEditorHint())
			return;

		isInteractingWithPlayer = false;
	}

	public override float GetLaunchRatio()
	{
		if (IsSleeping)
			return 0;

		if (IvyRatio > 0 && rotationVelocity > 0)
			return Mathf.Clamp(IvyRatio + 1, 0f, 1f);

		if (IvyRatio <= 0)
			return 0;

		return IvyRatio;
	}

	private void Respawn() => StartSleeping();

	private void StartSleeping()
	{
		targetImpulse = impulseVelocity = 0;
		rotationVelocity = 0;
		IvyRatio = 0;
		IsSleeping = true;
	}

	protected override void LaunchAnimation()
	{
		Player.Effect.StartSpinFX();
		Player.Animator.StartSpin(3.0f);

		// Update direction
		Player.PathFollower.SetActivePath(Player.PathFollower.ActivePath, ReversePathTemporarily);

		if (ReversePathTemporarily &&
			!Player.IsConnected(PlayerController.SignalName.LandedOnGround, ClearReversePathCallable))
		{
			// Connect signal so we can reset when we land
			Player.Connect(PlayerController.SignalName.LandedOnGround, ClearReversePathCallable, (int)ConnectFlags.OneShot);
		}
	}

	public void UnlinkReversePath()
	{
		if (!Player.IsConnected(PlayerController.SignalName.LandedOnGround, ClearReversePathCallable))
			return;

		Player.Disconnect(PlayerController.SignalName.LandedOnGround, ClearReversePathCallable);
	}

	public void ClearReversePath()
	{
		UnlinkReversePath();

		if (!Player.PathFollower.IsReversingPath)
			return;

		// Revert to the correct path direction
		Player.PathFollower.SetActivePath(Player.PathFollower.ActivePath, false);
		if (Player.IsOnGround) // Play turnaround animation if we're on the ground
			Player.CallDeferred(PlayerController.MethodName.StartReversePath);
	}

	/// <summary> Adds some force from the player. </summary>
	public void AddImpulseForce(float amount, bool isEntryForce = false)
	{
		if (isEntryForce)
		{
			float forwardAngle = ExtensionMethods.CalculateForwardAngle(this.Forward());
			if (ExtensionMethods.DeltaAngleRad(Player.MovementAngle, forwardAngle) >= Mathf.Pi * 0.5f)
				amount *= -1;
		}

		IsSleeping = false;
		targetImpulse = Mathf.Min(targetImpulse + amount, 1f);
	}

	public void AddGravity()
	{
		float gravityAmount = Gravity;
		int velocitySign = Mathf.Sign(rotationVelocity);
		if (Mathf.Sign(IvyRatio) == velocitySign)
		{
			if (isInteractingWithPlayer)
			{
				// Only apply heavy gravity when swinging far to keep swinging slowly when idle
				if (Mathf.Abs(IvyRatio) > .2f)
					gravityAmount *= GravityMultiplier;
			}
			else
			{
				// Kill speed quickly when not interacting with player
				rotationVelocity *= 0.8f;
				gravityAmount *= 0.8f;
			}
		}

		rotationVelocity -= Mathf.Sign(IvyRatio) * gravityAmount * PhysicsManager.physicsDelta; // Apply gravity

		if (velocitySign != Mathf.Sign(rotationVelocity))
			ChangeSwingDirection();
	}

	private void ChangeSwingDirection()
	{
		if (Mathf.Abs(IvyRatio) < 0.01f)
		{
			// Barely swinging; start sleeping again
			StartSleeping();
			return;
		}

		// Update swingSfx volume 
		swingSfx.VolumeLinear = Mathf.Abs(IvyRatio);
	}

	private void UpdateSwing()
	{
		targetImpulse = Mathf.MoveToward(targetImpulse, 0, ImpulseDecceleration * PhysicsManager.physicsDelta);
		impulseVelocity = Mathf.MoveToward(impulseVelocity, targetImpulse, ImpulseAcceleration * PhysicsManager.physicsDelta);

		rotationVelocity += impulseVelocity; // Add impulse velocity
		AddGravity();

		float rotationClampAmount = Mathf.Clamp(1f - Mathf.Abs(IvyRatio), 0f, 1f);
		float velocityClamp = MaxRotationSpeed * rotationClampAmount / lengthInfluence;
		rotationVelocity = Mathf.Clamp(rotationVelocity, -velocityClamp, velocityClamp);

		float targetRatio = IvyRatio + (rotationVelocity * lengthInfluence * PhysicsManager.physicsDelta);
		CheckSwingSfx(targetRatio);
		IvyRatio = Mathf.Clamp(targetRatio, -1f, 1f);

		if (!isInteractingWithPlayer)
		{
			if (Mathf.Abs(IvyRatio) < 0.01f && Mathf.Abs(rotationVelocity) < 0.01f)
			{
				IvyRatio = 0;
				rotationVelocity = 0;
				IsSleeping = true;
			}
			else if (Mathf.Abs(IvyRatio) < 0.05f && Mathf.Abs(rotationVelocity) < 0.5f &&
				Mathf.Sign(IvyRatio) == Mathf.Sign(rotationVelocity))
			{
				rotationVelocity *= 0.9f;
			}
		}
	}

	/// <summary> Checks whether we're switching sides, and plays the swing sound effect. </summary>
	private void CheckSwingSfx(float targetRatio)
	{
		if (Mathf.Sign(targetRatio) == Mathf.Sign(IvyRatio))
			return;

		swingSfx.Play();
	}

	#region Setup
	public void SetRotation()
	{
		float rotation = IndividualRotation * IvyRatio;

		for (int i = 0; i < ivyLinks.Count; i++)
			ivyLinks[i].RotationDegrees = Vector3.Left * rotation;

		UpdateAreaPosition();
	}

	/// <summary> Moves the area trigger to the last link's position. </summary>
	private void UpdateAreaPosition()
	{
		if (ivyLinks.Count == 0)
			return;

		launchPoint.GlobalTransform = ivyLinks[ivyLinks.Count - 1].GlobalTransform;
		launchPoint.GlobalPosition -= ivyLinks[ivyLinks.Count - 1].Up() * .5f;
	}

	private void Initialize()
	{
		if (launchPoint == null)
		{
			GD.PushError("Ivy launch Point could not be found.");
			return;
		}

		// Resize ivy to the proper length
		UpdateIvyLength();
		UpdateAreaPosition();
	}

	private void UpdateIvyLength()
	{
		if (ivyLinks.Count > Length)
		{
			// Since every ivy link is parented, we only need to delete one.
			ivyLinks[Length].QueueFree();
			ivyLinks.Resize(Length);
			return;
		}

		if (IvyScene == null)
		{
			GD.PushError("Ivy Scene could not be found.");
			return;
		}

		if (ivyLinks.Count == 0)
		{
			linkRoot = IvyScene.Instantiate<Node3D>();
			AddChild(linkRoot);
			ivyLinks.Add(linkRoot);
		}

		// Add ivy individually as needed
		while (ivyLinks.Count < Length)
		{
			Node3D linkNode = IvyScene.Instantiate<Node3D>();
			ivyLinks[ivyLinks.Count - 1].AddChild(linkNode); // Add as a child so rotations carry over
			linkNode.Position = Vector3.Down;
			ivyLinks.Add(linkNode);
		}
	}
	#endregion
}
