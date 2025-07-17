using Godot;
using Project.Core;

namespace Project.Gameplay;

/// <summary>
/// Renders multiplicative bloom. Used by the Lost Prologue's blocks.
/// </summary>
public partial class MultiplicativeBloomRenderer : Node
{
	[Export] private Camera3D bloomCamera;
	[Export] private Camera3D objectCamera;
	[Export] private SubViewport bloomViewport;
	[Export] private SubViewport objectViewport;
	[Export] private ShaderMaterial multiplicativeBloomMaterial;
	private Camera3D GameplayCamera => GetViewport().GetCamera3D();

	private Callable UpdatePositionCallable => new(this, MethodName.UpdatePosition);
	private Callable ApplyTextureCallable => new(this, MethodName.ApplyTexture);

	private readonly StringName BloomTexture = "bloom_texture";
	private readonly StringName ObjectTexture = "object_texture";

	public override void _Ready()
	{
		bloomCamera.Near = objectCamera.Near = 0.01f;
		bloomCamera.Far = objectCamera.Far = 100.0f; // Wonderblocks fade out by 50 units, so don't render any further
	}

	public override void _EnterTree()
	{
		bloomViewport.Size = GetTree().Root.Size;
		switch (SaveManager.Config.bloomMode)
		{
			case SaveManager.QualitySetting.Disabled:
				bloomCamera.ClearCurrent();
				objectCamera.ClearCurrent();
				return;
			case SaveManager.QualitySetting.Low: // Quarter resolution
				bloomViewport.Size /= 4;
				break;
			case SaveManager.QualitySetting.Medium: // Half resolution
				bloomViewport.Size /= 2;
				break;
		}
		objectViewport.Size = bloomViewport.Size;

		if (!RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePreDraw, UpdatePositionCallable))
			RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePreDraw, UpdatePositionCallable, (uint)ConnectFlags.Deferred);

		if (!RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable))
			RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable, (uint)ConnectFlags.Deferred);
	}

	public override void _ExitTree()
	{
		if (RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePreDraw, UpdatePositionCallable))
			RenderingServer.Singleton.Disconnect(RenderingServer.SignalName.FramePreDraw, UpdatePositionCallable);

		if (RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable))
			RenderingServer.Singleton.Disconnect(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable);

		ApplyTexture();
	}

	private void UpdatePosition()
	{
		bloomCamera.Fov = objectCamera.Fov = GameplayCamera.Fov;
		bloomCamera.Size = objectCamera.Size = GameplayCamera.Size;
		bloomCamera.Projection = objectCamera.Projection = GameplayCamera.Projection;

		bloomCamera.GlobalTransform = objectCamera.GlobalTransform = GameplayCamera.GlobalTransform;
	}

	private void ApplyTexture()
	{
		multiplicativeBloomMaterial.SetShaderParameter(BloomTexture, bloomViewport.GetTexture());
		multiplicativeBloomMaterial.SetShaderParameter(ObjectTexture, objectViewport.GetTexture());
	}

}
