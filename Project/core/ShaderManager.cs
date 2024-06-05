using Godot;
using Godot.Collections;

namespace Project.Core
{
	/// <summary> Handles shader precompilation before every stage can begin. </summary>
	public partial class ShaderManager : Control
	{
		public static ShaderManager Instance;
		public bool IsCompilingShaders { get; private set; }

		[Export]
		private SubViewport shaderCompilationViewport;
		[Export]
		private Node3D shaderParent;
		[Export]
		private MeshInstance3D[] meshInstances;
		[Export]
		private GpuParticles3D[] particles;

		private int meshCompilationIndex = 0;
		private int materialCompilationIndex = 0;
		private readonly Array<Mesh> meshes = new();
		private readonly Array<Material> materials = new();

		public override void _EnterTree() => Instance = this;


		public override void _Process(double _)
		{
			if (!IsCompilingShaders) return;

			if (materialCompilationIndex < materials.Count)
			{
				Material m = materials[materialCompilationIndex];
				if (m is ParticleProcessMaterial)
				{
					for (int i = 0; i < particles.Length; i++)
					{
						particles[i].ProcessMaterial = m;
						particles[i].Restart();
					}
				}
				else
				{
					for (int i = 0; i < particles.Length; i++)
						particles[i].MaterialOverride = m;
					for (int i = 0; i < meshInstances.Length; i++)
						meshInstances[i].MaterialOverride = m;
				}

				materialCompilationIndex++;
			}

			if (meshCompilationIndex < meshes.Count)
			{
				for (int i = 0; i < meshInstances.Length; i++)
					meshInstances[i].Mesh = meshes[meshCompilationIndex];
				meshCompilationIndex++;
			}

			if (materialCompilationIndex >= materials.Count && meshCompilationIndex >= meshes.Count)
				FinishCompilation();
		}


		public void StartCompilation()
		{
			// No shaders to compile!
			if (materials.Count == 0) return;

			GD.Print("Starting shader compilation.");


			Viewport mainViewport = GetViewport();
			shaderCompilationViewport.ScreenSpaceAA = mainViewport.ScreenSpaceAA;
			shaderCompilationViewport.Msaa3D = mainViewport.Msaa3D;
			shaderCompilationViewport.Scaling3DMode = mainViewport.Scaling3DMode;
			shaderCompilationViewport.Scaling3DScale = mainViewport.Scaling3DScale;
			shaderCompilationViewport.World3D.Environment = mainViewport.World3D.Environment;

			// Disable vsync
			if (DisplayServer.WindowGetVsyncMode() != DisplayServer.VSyncMode.Disabled)
				DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);

			Visible = shaderParent.Visible = true;
			materialCompilationIndex = 0;
			IsCompilingShaders = true;
		}


		public void FinishCompilation()
		{
			for (int i = 0; i < particles.Length; i++)
			{
				particles[i].Emitting = false;
				particles[i].ProcessMaterial = null;
				particles[i].MaterialOverride = null;
			}

			for (int i = 0; i < meshInstances.Length; i++)
				meshInstances[i].MaterialOverride = null;

			materials.Clear();
			meshes.Clear();
			IsCompilingShaders = false;
			Visible = shaderParent.Visible = false;

			// Reapply v-sync
			if (SaveManager.Config.useVsync)
				DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Enabled);

			GD.Print($"Shader compilation finished {materialCompilationIndex} materials compiled.");
		}


		public void QueueMaterial(Material material)
		{
			if (materials.Contains(material)) return;
			materials.Add(material);
		}


		public void QueueMesh(Mesh mesh)
		{
			if (meshes.Contains(mesh)) return;

			meshes.Add(mesh);
			for (int i = 0; i < mesh.GetSurfaceCount(); i++)
				QueueMaterial(mesh.SurfaceGetMaterial(i));

		}
	}
}
