using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay.Objects;

[Tool]
public partial class Ivy : Launcher
{
	[Signal] public delegate void IvyStartedEventHandler();

	[Export]
	public bool Regenerate
	{
		get => false;
		set
		{
			if (value)
				Initialize();
		}
	}

	[ExportGroup("Settings")]
	[Export(PropertyHint.Range, "2, 50")] public int length;
	[Export]
	private float maxRotation;
	private float IndividualRotation => maxRotation / length;

	[Export] public bool IsSleeping { get; private set; }
	[Export(PropertyHint.Range, "-1,1")]
	public float LaunchRatio
	{
		get => launchRatio;
		private set
		{
			launchRatio = value;
			SetRotation();
		}
	}

	[ExportGroup("Components")]
	[Export] private PackedScene ivyScene;
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
	private readonly float MaxRotationSpeed = 15.0f;

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint() || IsSleeping)
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

		if (LaunchRatio > 0 && rotationVelocity > 0)
			return Mathf.Clamp(LaunchRatio + 1, 0f, 1f);

		if (LaunchRatio <= 0)
			return 0;

		return LaunchRatio;
	}

	protected override void SetUp()
	{
		Initialize();

		if (Engine.IsEditorHint())
			return;

		IsSleeping = true;

		// Adjust swing speed based on length (longer ivys swing slower)
		lengthInfluence = Mathf.Clamp(8f / length, 0.5f, 1f);
		StageSettings.Instance.Respawned += Respawn;
	}

	private void Respawn()
	{
		targetImpulse = impulseVelocity = 0;
		rotationVelocity = 0;
		LaunchRatio = 0;
		IsSleeping = true;
	}

	protected override void LaunchAnimation()
	{
		Player.Effect.StartSpinFX();
		Player.Animator.StartSpin(3.0f);
	}

	/// <summary> Adds some force from the player. </summary>
	public void AddImpulseForce(float amount)
	{
		targetImpulse = Mathf.Clamp(targetImpulse + amount, 0f, 1f);
		IsSleeping = false;
	}

	public void AddGravity()
	{
		float gravityAmount = Gravity;
		if (Mathf.Sign(LaunchRatio) == Mathf.Sign(rotationVelocity))
		{
			// Kill speed quickly when not interacting with player
			if (!isInteractingWithPlayer)
				rotationVelocity *= 0.9f;

			gravityAmount *= GravityMultiplier;
		}

		rotationVelocity -= Mathf.Sign(LaunchRatio) * gravityAmount * PhysicsManager.physicsDelta; // Apply gravity
	}

	private void UpdateSwing()
	{
		targetImpulse = Mathf.MoveToward(targetImpulse, 0, ImpulseDecceleration * PhysicsManager.physicsDelta);
		impulseVelocity = Mathf.MoveToward(impulseVelocity, targetImpulse, ImpulseAcceleration * PhysicsManager.physicsDelta);

		rotationVelocity += impulseVelocity; // Add impulse velocity
		AddGravity();

		float rotationClampAmount = Mathf.Clamp(1f - Mathf.Abs(LaunchRatio), 0f, 1f);
		if (Mathf.Sign(LaunchRatio) == Mathf.Sign(rotationVelocity))
			rotationVelocity = Mathf.Min(rotationVelocity, MaxRotationSpeed * rotationClampAmount);

		float targetRatio = LaunchRatio + rotationVelocity * lengthInfluence * PhysicsManager.physicsDelta;
		LaunchRatio = Mathf.Clamp(targetRatio, -1f, 1f);

		if (Mathf.IsZeroApprox(targetImpulse))
		{
			if (Mathf.Abs(LaunchRatio) < 0.01f && Mathf.Abs(rotationVelocity) < 0.01f)
			{
				LaunchRatio = 0;
				rotationVelocity = 0;
				IsSleeping = true;
			}
			else if (Mathf.Abs(LaunchRatio) < 0.05f && Mathf.Abs(rotationVelocity) < 0.5f &&
				Mathf.Sign(LaunchRatio) == Mathf.Sign(rotationVelocity))
			{
				rotationVelocity *= 0.9f;
			}
		}
	}

	#region Setup
	public void SetRotation()
	{
		float rotation = IndividualRotation * launchRatio;

		for (int i = 0; i < ivyLinks.Count; i++)
			ivyLinks[i].RotationDegrees = Vector3.Left * rotation;

		UpdateAreaPosition();
	}

	/// <summary> Moves the area trigger to the last link's position. </summary>
	private void UpdateAreaPosition()
	{
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
		if (ivyLinks.Count > length)
		{
			// Since every ivy link is parented, we only need to delete one.
			ivyLinks[length].QueueFree();
			ivyLinks.Resize(length);
			return;
		}

		if (ivyScene == null)
		{
			GD.PushError("Ivy Scene could not be found.");
			return;
		}

		if (ivyLinks.Count == 0)
		{
			linkRoot = ivyScene.Instantiate<Node3D>();
			AddChild(linkRoot);
			ivyLinks.Add(linkRoot);
		}

		// Add ivy individually as needed
		while (ivyLinks.Count < length)
		{
			Node3D linkNode = ivyScene.Instantiate<Node3D>();
			ivyLinks[ivyLinks.Count - 1].AddChild(linkNode); // Add as a child so rotations carry over
			linkNode.Position = Vector3.Down;
			ivyLinks.Add(linkNode);
		}
	}
	#endregion
}
