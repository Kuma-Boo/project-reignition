using Godot;

namespace Project.Gameplay.Objects;

public partial class SkyRoad : Node3D
{
	[Export] private MeshInstance3D mesh;
	[Export] private Path3D path;
	public Path3D Path => path;

	private readonly StringName PathLengthParameter = "path_length";
	private readonly StringName PathRatioParameter = "path_ratio";

	public override void _Ready()
	{
		mesh.SetInstanceShaderParameter(PathLengthParameter, path.Curve.GetBakedLength());
		SetPathRatio(0.0f);
	}

	public void SetPathRatio(float ratio) => mesh.SetInstanceShaderParameter(PathRatioParameter, ratio);
}
