using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay;

/// <summary>
/// Renders planar reflections into a global texture, then updates materials in reflectionMaterials.
/// To have multiple reflections at different elevations, create multiple PlanarReflectionRenderers
/// and use separate materials for each.
/// </summary>
public partial class PlanarReflectionRenderer : Node3D
{
	// All shaders that use reflections must have this parameter
	public static readonly StringName ReflectionParameter = "reflection_texture";

	[Export] private bool disableRenderering;
	private bool checkpointRenderingStateDisabled;

	[Export] private Camera3D reflectionCamera;
	[Export] private SubViewport reflectionViewport;
	[Export] private SubViewportContainer reflectionViewportContainer;
	private Camera3D GameplayCamera => GetViewport().GetCamera3D();

	[Export] private HeightMode heightMode;
	private enum HeightMode
	{
		Static, // Move the planar reflection manually
		MatchPlayer, // Match the player's height
		MatchPlayerWithRaycast, // Raycast downwards, then match the height. Expensive.
	}

	/// <summary> List of materials that use reflection_texture. </summary>
	[Export] public Array<ShaderMaterial> reflectionMaterials;

	private Callable UpdatePositionCallable => new(this, MethodName.UpdatePosition);
	private Callable ApplyTextureCallable => new(this, MethodName.ApplyTexture);

	public override void _Input(InputEvent e)
	{
		if (!OS.IsDebugBuild())
			return;

		if (Input.IsActionJustPressed("debug_reflection"))
			reflectionViewportContainer.Visible = !reflectionViewportContainer.Visible;
	}

	public override void _EnterTree()
	{
		reflectionViewport.Size = SaveManager.WindowSizes[SaveManager.Config.windowSize];
		switch (SaveManager.Config.reflectionQuality)
		{
			case SaveManager.QualitySetting.Disabled:
				reflectionCamera.ClearCurrent();
				return;
			case SaveManager.QualitySetting.Low: // Quarter resolution
				reflectionViewport.Size /= 4;
				break;
			case SaveManager.QualitySetting.Medium: // Half resolution
				reflectionViewport.Size /= 2;
				break;
		}

		if (!RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePreDraw, UpdatePositionCallable))
			RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePreDraw, UpdatePositionCallable, (uint)ConnectFlags.Deferred);

		if (!RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable))
			RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable, (uint)ConnectFlags.Deferred);

		StageSettings.Instance.TriggeredCheckpoint += SaveCheckpointState;
		StageSettings.Instance.Respawned += LoadCheckpointState;

		checkpointRenderingStateDisabled = disableRenderering;
		LoadCheckpointState();
	}

	public override void _ExitTree()
	{
		if (RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePreDraw, UpdatePositionCallable))
			RenderingServer.Singleton.Disconnect(RenderingServer.SignalName.FramePreDraw, UpdatePositionCallable);

		if (RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable))
			RenderingServer.Singleton.Disconnect(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable);

		ApplyTexture();
	}

	// Mirror main camera along plane
	private void UpdatePosition()
	{
		if (disableRenderering)
			return;

		reflectionCamera.CullMask = GameplayCamera.CullMask;
		reflectionCamera.Fov = GameplayCamera.Fov;
		reflectionCamera.Size = GameplayCamera.Size;
		reflectionCamera.Projection = GameplayCamera.Projection;

		reflectionCamera.Near = GameplayCamera.Near;
		reflectionCamera.Far = GameplayCamera.Far;

		// Calculate reflection position
		Vector3 reflectionPosition = CalculateReflectionPosition();

		// Update reflectionCamera's position
		Vector3 reflectionAxis = this.Up();
		Vector3 projection = reflectionAxis * reflectionAxis.Dot(GameplayCamera.GlobalPosition - reflectionPosition);
		Vector3 targetPosition = GameplayCamera.GlobalPosition - (projection * 2f);

		// Update reflectionCamera's rotation
		Vector3 upDirection = GameplayCamera.Up().Reflect(reflectionAxis.Normalized());
		Vector3 forwardDirection = GameplayCamera.Forward().Reflect(reflectionAxis.Normalized());

		reflectionCamera.LookAtFromPosition(targetPosition, targetPosition + forwardDirection, upDirection);

		if (Engine.IsEditorHint())
			reflectionViewport.Size = (Vector2I)GameplayCamera.GetViewport().GetVisibleRect().Size;
		else
			reflectionViewport.Size = Runtime.HalfScreenSize;

		reflectionViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
	}

	private readonly int GroundCheckLength = 50;
	private Vector3 CalculateReflectionPosition()
	{
		Vector3 returnValue = GlobalPosition;
		if (heightMode == HeightMode.MatchPlayerWithRaycast)
		{
			RaycastHit hit = this.CastRay(StageSettings.Player.CenterPosition, Vector3.Down * GroundCheckLength, Runtime.Instance.environmentMask);
			if (hit)
				return hit.point;
		}
		else if (heightMode == HeightMode.MatchPlayer)
		{
			returnValue.Y = StageSettings.Player.GlobalPosition.Y;
		}

		return GlobalPosition;
	}

	/// <summary> Applies reflection texture to associated shaders. </summary>
	private void ApplyTexture()
	{
		if (disableRenderering)
			return;

		ViewportTexture ReflectionTexture = reflectionViewport.GetTexture();

		if (reflectionMaterials != null)
		{
			for (int i = 0; i < reflectionMaterials.Count; i++)
				reflectionMaterials[i].SetShaderParameter(ReflectionParameter, ReflectionTexture);
		}
	}

	private void SaveCheckpointState() => checkpointRenderingStateDisabled = disableRenderering;
	private void LoadCheckpointState()
	{
		if (checkpointRenderingStateDisabled)
			DisableRendering();
		else
			EnableRendering();
	}

	/// <summary> Call these from signals to save rendering resources. </summary>
	public void DisableRendering()
	{
		reflectionCamera.ClearCurrent();
		reflectionCamera.ProcessMode = ProcessModeEnum.Disabled;
		reflectionViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
		disableRenderering = true;
	}
	public void EnableRendering()
	{
		reflectionCamera.MakeCurrent();
		reflectionCamera.ProcessMode = ProcessModeEnum.Inherit;
		disableRenderering = false;
	}
}
