@tool
extends VBoxContainer

var current_texture : AnimatedTexture
@onready var load_text : LineEdit = get_node("Source");
@onready var filter_text : LineEdit = get_node("Filter");
@onready var framerate_spin : SpinBox = get_node("Framerate");
@onready var file_format : OptionButton = get_node("Format");
@onready var loop_type : OptionButton = get_node("LoopType");

func update_current_transform(object):
	current_texture = object
	file_format.clear()
	file_format.add_item("PNG")
	loop_type.clear()
	loop_type.add_item("Loop")
	loop_type.add_item("Ping Pong")
	loop_type.add_item("One Shot")

func LoadFrames():
	if !load_text.text.ends_with('/'):
		print("invalid path")
		return

	var files = GetFiles()
	var frame_duration : float = 1.0 / framerate_spin.value;

	current_texture.frames = files.size()
	
	if loop_type.selected == 0:
		current_texture.one_shot = false
	elif loop_type.selected == 2:
		current_texture.one_shot = true

	for i in files.size():
		var tex = load(load_text.text + files[i]) as Texture2D
		current_texture.set_frame_texture(i, tex)
		current_texture.set_frame_duration(i, frame_duration)
	
	if loop_type.selected == 1: #Loop ping pong
		current_texture.frames += files.size() - 2
		
		for i in files.size() - 2:
			var tex = current_texture.get_frame_texture(files.size() - (i + 1))
			current_texture.set_frame_texture(files.size() + i, tex)
			current_texture.set_frame_duration(files.size() + i, frame_duration)

func GetFiles() -> Array:
	var files = []
	
	var dir = DirAccess.open(load_text.text)
	dir.list_dir_begin()

	while true:
		var file = dir.get_next()
		if file == "":
			break

		if ValidFile(file):
			files.append(file)

	dir.list_dir_end()

	return files

func ValidFile(file : String) -> bool:
	if !file.to_lower().ends_with(file_format.get_item_text(file_format.selected).to_lower()):
		return false

	if file.begins_with("."):
		return false

	if !filter_text.text.is_empty() && !file.begins_with(filter_text.text):
		return false

	return true
