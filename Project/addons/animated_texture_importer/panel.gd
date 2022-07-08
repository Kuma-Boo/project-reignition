tool
extends VBoxContainer

var current_texture : AnimatedTexture
onready var load_text : LineEdit = get_node("Path");
onready var filter_text : LineEdit = get_node("Filter");
onready var file_format : OptionButton = get_node("Format");

func update_current_transform(object):
	current_texture = object
	file_format.clear()
	file_format.add_item("PNG")

func LoadFrames():
	if !load_text.text.ends_with('/'):
		print("invalid path")
		return

	var files = GetFiles()

	current_texture.frames = files.size()

	for i in files.size():
		var tex = load(load_text.text + files[i]) as Texture
		current_texture.set_frame_texture(i, tex)

func GetFiles() -> Array:
	var files = []
	var dir = Directory.new()

	dir.open(load_text.text)
	dir.list_dir_begin()

	while true:
		var file = dir.get_next()
		if file == "":
			break

		if ValidFile(file):
			files.append(file)

	dir.list_dir_end()

	return files

func ValidFile(var file : String) -> bool:
	if !file.to_lower().ends_with(file_format.get_item_text(file_format.selected).to_lower()):
		return false

	if file.begins_with("."):
		return false

	if !filter_text.text.empty() && !file.begins_with(filter_text.text):
		return false

	return true


