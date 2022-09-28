extends EditorInspectorPlugin

const TransformPanel = preload("res://addons/curve_edit/transform_panel.tscn")
var panel_instance


func can_handle(object):
	# Only paths are supported.
	return object is Path3D or object is Path2D


## Add the custom draw under the object-level transform panel.
func parse_property(object, type, path, hint, hint_text, usage):
	# print(path, usage)
	if path == "position": # Place directly after the obj-scale transform
		panel_instance = TransformPanel.instantiate()
		add_custom_control(panel_instance)
		panel_instance.call_deferred("update_current_transform", object)
	return false

