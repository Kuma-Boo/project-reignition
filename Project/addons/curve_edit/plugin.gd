@tool
extends EditorPlugin


var plugin


func _enter_tree():
	plugin = preload("res://addons/curve_edit/path_edit.gd").new()
	add_inspector_plugin(plugin)


func _exit_tree():
	remove_inspector_plugin(plugin)


func refresh() -> void:
	get_editor_interface().get_inspector().refresh()
