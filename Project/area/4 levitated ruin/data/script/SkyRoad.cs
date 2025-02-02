using Godot;

namespace Project.Gameplay.Objects;

public partial class SkyRoad : Node3D
{
	[Export] private MeshInstance3D mesh;
	[Export] private float totalPathLength;

	private readonly StringName PathLengthParameter = "path_length";
	private readonly StringName PathRatioParameter = "path_ratio";

	public override void _Ready()
	{
		// Update total path length
		mesh.SetInstanceShaderParameter(PathLengthParameter, totalPathLength);
	}

	public void SetPathRatio(float ratio) => mesh.SetInstanceShaderParameter(PathRatioParameter, ratio);
}
