using Godot;

public partial class SkyRoad : Node3D
{
	[Export] private MeshInstance3D mesh;
	[Export] private Path3D path;
	// TODO Add gargoyle [Export] private GargoyleTraveller gargoyle;

	public override void _Ready()
	{
		mesh.SetInstanceShaderParameter("path_length", path.Curve.GetBakedLength());
		mesh.SetInstanceShaderParameter("path_ratio", 0.0f);
	}
}
