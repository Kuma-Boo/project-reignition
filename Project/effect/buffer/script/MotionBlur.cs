using Godot;

namespace Project.Core;

[Tool]
public partial class MotionBlur : MeshInstance3D
{
	[Export]
	private ShaderMaterial material;
	[Export]
	private Camera3D camera;

	private Vector3 previousCameraPosition;
	private Quaternion previousCameraRotation;

	private readonly string LinearVelocityParameter = "linear_velocity";
	private readonly string AngularVelocityParameter = "angular_velocity";

	public override void _PhysicsProcess(double _)
	{
		if (GetTree().Paused)
			return;

		if (material == null || camera == null)
			return;

		material.SetShaderParameter(LinearVelocityParameter, CalculateLinearVelocity());
		material.SetShaderParameter(AngularVelocityParameter, CalculateAngularVelocity());

		previousCameraPosition = camera.GlobalPosition;
		previousCameraRotation = camera.GlobalBasis.GetRotationQuaternion();
	}

	private Vector3 CalculateLinearVelocity() => camera.GlobalPosition - previousCameraPosition;
	private Vector3 CalculateAngularVelocity()
	{
		Quaternion rotation = camera.GlobalBasis.GetRotationQuaternion();
		Quaternion rotationDifference = rotation - previousCameraRotation;
		Quaternion rotationConjugate = new(-rotation.X, -rotation.Y, -rotation.Z, rotation.W);
		Quaternion angularRotation = rotationDifference * 2.0f * rotationConjugate;
		return new Vector3(angularRotation.X, angularRotation.Y, angularRotation.Z);
	}
}
