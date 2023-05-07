extends EditorInspectorPlugin

const TransformPanel = preload("res://addons/animated_texture_importer/panel.tscn")
var panel_instance

func _can_handle(object):
	return object is AnimatedTexture

func _parse_property(object, type, path, hint, hint_text, usage, wide) -> bool:
	print(path)
	if path == "frames":
		panel_instance = TransformPanel.instantiate()
		add_custom_control(panel_instance)
		panel_instance.call_deferred("update_current_transform", object)
	return false

