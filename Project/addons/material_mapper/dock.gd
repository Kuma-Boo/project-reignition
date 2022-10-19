@tool
extends Control

@onready var texturePath : LineEdit = get_node("Texture/Path")
@onready var materialPath : LineEdit = get_node("Material/Path")
@onready var textureNameFormat : OptionButton = get_node("TextureNameFormat/Format")

@onready var importAlbedo : Button = get_node("Toggles/ImportAlbedo")
@onready var importNormal : Button = get_node("Toggles/ImportNormal")
@onready var importRoughness : Button = get_node("Toggles/ImportRoughness")
@onready var resetSpecular : Button = get_node("Toggles/ResetSpecular")
@onready var clearMissingTextures : Button = get_node("Toggles/ClearMissingTextures")

var materials : Array
var textures : Array

const MATERIAL_EXTENSTION : String = ".tres"
const TEXTURE_EXTENSTION : String = ".png"

var currentMaterial : StandardMaterial3D
var currentMaterialName : String

func _ready():
	textureNameFormat.clear()
	textureNameFormat.add_item("Keep")
	textureNameFormat.add_item("Lowercase")
	textureNameFormat.add_item("Uppercase")
	textureNameFormat.select(1)

func _on_update_pressed():
	if texturePath.text.is_empty():
		printerr("Texture directory cannot be empty!")
		return

	if materialPath.text.is_empty():
		printerr("Material directory cannot be empty!")
		return

	update_materials()


func update_materials():
	materials = get_files_in_directory(materialPath.text, MATERIAL_EXTENSTION)
	textures = get_files_in_directory(texturePath.text, TEXTURE_EXTENSTION)

	for i in materials:
		var mat = load(str(materialPath.text) + i)
		if not mat is StandardMaterial3D:
			continue #Custom Shader materials should be edited manually!

		currentMaterial = mat

		#Default to no specular?
		if resetSpecular.pressed:
			currentMaterial.metallic_specular = 0;

		var materialName = str(i).replace(MATERIAL_EXTENSTION, "")
		currentMaterialName = materialName
		if textureNameFormat.selected == 1:
			materialName = materialName.to_lower()
		elif textureNameFormat.selected == 2:
			materialName = materialName.to_upper()
		
		process_material(materialName)

func process_material(materialName : String):
	var textureIndex : int;
	if importAlbedo.pressed:
		textureIndex = find_texture(materialName, "")
		apply_texture(textureIndex, "albedo")


func apply_texture(textureIndex : int, map : String):
	map += "_texture"

	if textureIndex != -1:
		var texture : String = texturePath.text + textures[textureIndex]
		var image : CompressedTexture2D = load(texture)

		if map == "albedo_texture":
			currentMaterial.albedo_texture = image
		elif map == "normal_texture":
			currentMaterial.normal_enabled = true
			currentMaterial.normal_texture = image
		else:
			currentMaterial.roughness_texture = image
		print(map + " set to " + texture)
	elif clearMissingTextures.pressed:
		if map == "albedo_texture":
			currentMaterial.albedo_texture = null
		elif map == "normal_texture":
			currentMaterial.normal_enabled = false
			currentMaterial.normal_texture = null
		else:
			currentMaterial.roughness_texture = null
		print(map + " cleared for " + currentMaterialName)
	else:
		printerr(map + " not found for " + currentMaterialName)

func find_texture(fileName : String, map : String) -> int:
	return textures.find(fileName + map + TEXTURE_EXTENSTION)

func get_files_in_directory(path, extension) -> Array:
	var files = []
	var dir = DirAccess.open(path)
	if dir.list_dir_begin() == OK:
		while true:
			var file = dir.get_next()
			if file == "":
				break
			elif file.ends_with(extension):
				files.append(file)

		dir.list_dir_end()
	else:
		printerr("Error loading path " + path)
	
	return files
