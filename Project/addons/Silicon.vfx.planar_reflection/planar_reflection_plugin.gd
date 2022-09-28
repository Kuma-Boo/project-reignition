@tool
extends EditorPlugin

const folder = "res://addons/Silicon.vfx.planar_reflection/"

var editor_camera : Camera3D

func _ready() -> void:
	name = "PlanarReflectionPlugin"
	add_autoload_singleton("ReflectMaterialManager", folder + "reflect_material_manager.gd")

	# There's this quirk where the icon's import file isn't immemiately loaded.
	# This will loop until that file is generated , i.e. it can be loaded in.
	var icon : CompressedTexture2D
	var no_import := false
	while not icon:
		icon = load(folder + "planar_reflector_icon.svg")
		if not icon:
			no_import = true
			await get_tree().idle_frame
	if no_import:
		print("Ignore the errors above. This is normal.")

	add_custom_type("PlanarReflector", "MeshInstance3D",
			load(folder + "planar_reflector.gd"), icon
	)

	print("planar reflection plugin enter tree")

func _exit_tree():
	remove_custom_type("PlanarReflector")
	remove_autoload_singleton("ReflectMaterialManager")

	print("planar reflection plugin exit tree")

func _forward_3d_gui_input(p_camera : Camera3D, p_event : InputEvent) -> int:
	if not editor_camera:
		editor_camera = p_camera
	return EditorPlugin.AFTER_GUI_INPUT_PASS

func _handles(object):
	return true
