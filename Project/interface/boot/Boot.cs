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
		private MeshInstance3D mesh;

		public override void _Ready() => CompileMaterials();


		private async void CompileMaterials()
		{
			// Get a list of all tres files in the project
			List<string> tresFiles = GetTresFilePaths(RESOURCE_DIRECTORY);

			for (int i = 0; i < tresFiles.Count; i++)
			{
				if (!ResourceLoader.Exists(tresFiles[i], MATERIAL_HINT))
					continue;

				shaderCacheText.Text = $"{Tr("shader_cache_load")} ({i}/{tresFiles.Count})...";
				mesh.MaterialOverride = ResourceLoader.Load<Material>(tresFiles[i], MATERIAL_HINT);
				await ToSignal(GetTree().CreateTimer(PhysicsManager.normalDelta, false), SceneTreeTimer.SignalName.Timeout);
			}

			shaderCacheText.Text = $"{Tr("shader_cache_finished")}.";

			await ToSignal(GetTree().CreateTimer(.5f, false), SceneTreeTimer.SignalName.Timeout);
			animator.Play("shader_cache");
		}



		private readonly string RESOURCE_DIRECTORY = "res://";
		private readonly string MATERIAL_HINT = "Material";
		private readonly string MATERIAL_FOLDER = "material";
		private readonly string MATERIAL_EXTENSION = ".tres";

		private List<string> GetTresFilePaths(string path)
		{
			List<string> materialPaths = new();

			DirAccess directory = DirAccess.Open(path);
			directory.ListDirBegin();
			string fileName = directory.GetNext();
			while (!string.IsNullOrEmpty(fileName))
			{
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
