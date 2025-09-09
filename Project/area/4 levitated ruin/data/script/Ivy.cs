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
	[ExportToolButton("Regenerate Ivy")]
	public Callable GenerateIvyGroup => Callable.From(GenerateIvy);
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
	[Export] private NodePath root;
	private Node3D rootNode;
	private Array<Node3D> ivyLinks = [];
	private float lengthInfluence;

	private bool isInteractingWithPlayer;

	/// <summary> Ratio of velocity that gets applied, sampled based on the ivy's current ratio. </summary>
	[Export] private Curve velocityLimitCurve;
	/// <summary> How quickly the ivy is currently rotating. </summary>
	private float rotationVelocity;
	/// <summary> The maximum speed at which the ivy can rotate. </summary>
	[Export] private float rotationLimit;

	/// <summary> Amount of gravity to add. </summary>
	[Export] private float gravity;
	private float ratioLimit;
	private bool canChangeRatioLimit;

	protected override void SetUp()
	{
		GenerateIvy();

		if (Engine.IsEditorHint())
			return;

		base.SetUp();
		IsSleeping = true;

		// Adjust swing speed based on length (longer ivys swing slower)
		lengthInfluence = 1 / Mathf.Lerp(1f, 3f, Mathf.Min(Length / 50f, 1f));
		StageSettings.Instance.Respawned += Respawn;
	}

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint())
		{
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

		if (IvyRatio <= 0 || rotationVelocity < 0)
			return 0;

		return IvyRatio;
	}

	private void Respawn() => StartSleeping();

	private void StartSleeping()
	{
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
		rotationVelocity += amount * lengthInfluence;
		ratioLimit = 1f;
	}

	private void ChangeSwingDirection()
	{
		// Update swingSfx volume 
		swingSfx.VolumeLinear = Mathf.Abs(IvyRatio);
	}

	private void UpdateSwing()
	{
		ProcessGravity();
		ProcessLimitRotation();
		ProcessRotationVelocity();

		IvyRatio += (lengthInfluence * rotationVelocity * PhysicsManager.physicsDelta);
		IvyRatio = Mathf.Clamp(IvyRatio, -1f, 1f);

		CheckSwingSfx(IvyRatio);
	}

	private void ProcessGravity()
	{
		if (Mathf.IsZeroApprox(ratioLimit))
		{
			// Return to stationary position
			IvyRatio *= 0.9f;
			if (IvyRatio < 0.05f * lengthInfluence)
				IsSleeping = true;

			return;
		}

		// Add gravity (amount is more intense towards edges of the ivy's ratio)
		rotationVelocity += gravity * lengthInfluence * -IvyRatio * PhysicsManager.physicsDelta;
	}

	private void ProcessLimitRotation()
	{
		if (Mathf.Sign(IvyRatio) != Mathf.Sign(rotationVelocity))
		{
			canChangeRatioLimit = true;
			return;
		}

		if (!canChangeRatioLimit)
			return;

		// Flipped direction--update ratio limits
		canChangeRatioLimit = false;
		ratioLimit *= 0.8f;
		if (ratioLimit < 0.05f * lengthInfluence)
			ratioLimit = 0f;

	}

	private void ProcessRotationVelocity()
	{
		if (Mathf.IsZeroApprox(ratioLimit))
		{
			rotationVelocity = 0f;
			return;
		}

		if (Mathf.Sign(IvyRatio) != Mathf.Sign(rotationVelocity)) // Falling -- don't clamp speed
			return;

		float currentSampleRatio = Mathf.Clamp(Mathf.Abs(IvyRatio) / ratioLimit, 0f, 1f);
		float velocityClamp = rotationLimit * velocityLimitCurve.Sample(currentSampleRatio);
		rotationVelocity = Mathf.Clamp(rotationVelocity, -velocityClamp, velocityClamp);
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
		{
			ivyLinks[i].RotationDegrees = Vector3.Left * (i + 1) * rotation;

			if (i != 0)
				ivyLinks[i].Position = ivyLinks[i - 1].Position + ivyLinks[i - 1].Basis * Vector3.Down;
		}

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

	private void GenerateIvy()
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
		if (IvyScene == null)
		{
			GD.PushError("Ivy Scene could not be found.");
			return;
		}

		rootNode = GetNodeOrNull<Node3D>(root);
		if (rootNode == null)
		{
			GD.PushError("Ivy Root Node not found.");
			return;
		}

		// Clear children
		ivyLinks.Clear();
		foreach (Node child in rootNode.GetChildren())
			child.QueueFree();

		// Add ivy segments as needed
		for (int i = 0; i < Length; i++)
		{
			Node3D linkNode = IvyScene.Instantiate<Node3D>();
			rootNode.AddChild(linkNode);
			linkNode.Position = i * Vector3.Down;
			ivyLinks.Add(linkNode);
		}
	}
	#endregion
}
