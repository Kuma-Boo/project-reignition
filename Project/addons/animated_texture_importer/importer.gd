extends EditorInspectorPlugin

const TransformPanel = preload("res://addons/animated_texture_importer/panel.tscn")
var panel_instance


func can_handle(object):
	return object is AnimatedTexture


func parse_property(object, type, path, hint, hint_text, usage):
	print(path)
	if path == "frames":
		panel_instance = TransformPanel.instance()
		add_custom_control(panel_instance)
		panel_instance.call_deferred("update_current_transform", object)
	return false

