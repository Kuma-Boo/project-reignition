using Godot;
using Project.Core;
using System.Collections.Generic;

namespace Project.Interface
{
	public partial class Boot : Node
	{
		[Export]
		private AnimationPlayer animator;
		[Export]
		private Label shaderCacheText;
		[Export]
		private MeshInstance3D[] meshInstances;
		[Export]
		private GpuParticles3D particles;

		private int shaderCompilationIndex = -1;
		private List<string> tresFiles = new();

		public override void _Ready()
		{
			// Get a list of all tres files in the project
			tresFiles = GetTresFilePaths(RESOURCE_DIRECTORY);
			shaderCompilationIndex = 0;
			animator.Play("shader_cache_start");
		}


		public override void _Process(double _)
		{
			if (shaderCompilationIndex == -1) return;

			if (ResourceLoader.Exists(tresFiles[shaderCompilationIndex], MATERIAL_HINT))
			{
				shaderCacheText.Text = $"{Tr("shader_cache_load")} ({shaderCompilationIndex}/{tresFiles.Count})...";
				Material m = ResourceLoader.Load<Material>(tresFiles[shaderCompilationIndex], MATERIAL_HINT);
				if (m is ParticleProcessMaterial)
				{
					particles.ProcessMaterial = m;
					particles.Restart();
				}
				else
				{
					particles.MaterialOverride = m;

					for (int j = 0; j < meshInstances.Length; j++)
						meshInstances[j].MaterialOverride = m;
				}
			}

			shaderCompilationIndex++;
			if (shaderCompilationIndex >= tresFiles.Count)
			{
				shaderCompilationIndex = -1;
				shaderCacheText.Text = $"{Tr("shader_cache_finished")}.";
				animator.Play("shader_cache_finish");
			}
		}


		private readonly string RESOURCE_DIRECTORY = "res://";
		private readonly string MATERIAL_HINT = "Material";
		private readonly string MATERIAL_FOLDER = "material";
		private readonly string MATERIAL_EXTENSION = ".tres";
		private readonly string EXPORTED_EXTENSION = ".remap";

		private List<string> GetTresFilePaths(string path)
		{
			List<string> materialPaths = new();

			DirAccess directory = DirAccess.Open(path);
			directory.ListDirBegin();
			string fileName = directory.GetNext();
			while (!string.IsNullOrEmpty(fileName))
			{
				if (fileName.EndsWith(EXPORTED_EXTENSION))
					fileName = fileName.Replace(EXPORTED_EXTENSION, string.Empty);

				string filePath = path + "/" + fileName;
				if (directory.CurrentIsDir())
					materialPaths.AddRange(GetTresFilePaths(filePath));
				else if (fileName.EndsWith(MATERIAL_EXTENSION) && filePath.Contains(MATERIAL_FOLDER))
					materialPaths.Add(filePath);

				fileName = directory.GetNext();
			}

			return materialPaths;
		}


		private void StartTitleTransition()
		{
			TransitionManager.QueueSceneChange("res://interface/menu/Menu.tscn");
			TransitionManager.StartTransition(new()
			{
				inSpeed = .5f,
				outSpeed = .5f,
				color = Colors.Black
			});
		}
	}
}
