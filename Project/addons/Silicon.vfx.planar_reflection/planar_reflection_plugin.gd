tool
extends EditorPlugin

const folder = "res://addons/Silicon.vfx.planar_reflection/"

var editor_camera : Camera

func _ready() -> void:
	name = "PlanarReflectionPlugin"
	add_autoload_singleton("ReflectMaterialManager", folder + "reflect_material_manager.gd")

	# There's this quirk where the icon's import file isn't immemiately loaded.
	# This will loop until that file is generated , i.e. it can be loaded in.
	var icon : StreamTexture
	var no_import := false
	while not icon:
		icon = load(folder + "planar_reflector_icon.svg")
		if not icon:
			no_import = true
			yield(get_tree(), "idle_frame")
	if no_import:
		print("Ignore the errors above. This is normal.")

	add_custom_type("PlanarReflector", "MeshInstance",
			load(folder + "planar_reflector.gd"), icon
	)

	print("planar reflection plugin enter tree")

func _exit_tree():
	remove_custom_type("PlanarReflector")
	remove_autoload_singleton("ReflectMaterialManager")

	print("planar reflection plugin exit tree")

func forward_spatial_gui_input(p_camera : Camera, p_event : InputEvent) -> bool:
	if not editor_camera:
		editor_camera = p_camera
	return false

func handles(object):
	return true
