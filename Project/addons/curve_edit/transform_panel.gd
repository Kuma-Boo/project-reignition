# Panel which is added to UI and used to trigger callbacks to update paths.
tool
extends VBoxContainer

# Index aliases
const POS = 0;
const IN = 1;
const OUT = 2;

onready var point_index:OptionButton = get_node("HBoxContainer2/point_selector")
onready var point_part:OptionButton = get_node("HBoxContainer2/point_part")
onready var edit_x:LineEdit = get_node("HBoxContainer/x_edit")
onready var edit_y:LineEdit = get_node("HBoxContainer/y_edit")
onready var edit_z:LineEdit = get_node("HBoxContainer/z_edit")

var current_path:Path
var backup_curve:Curve3D
var backup_path:WeakRef

var _internal_update := false
var _curve_cache = [] # Used to detect which point recently modified.


func _ready():
	point_part.clear()
	point_part.add_item("Position")
	point_part.add_item("In")
	point_part.add_item("Out")


func _input(event):
	if not (event is InputEventKey and event.pressed):
		return
	if not event.scancode == KEY_ENTER:
		return
	var focus = get_focus_owner()
	if focus == edit_x or focus == edit_y or focus == edit_z:
		_on_edit_focus_exited()
		print("Pressed enter")


# Update the dropdown for points and current values of line edits,
# for when the curve is modified externally.
func update_current_transform(object):
	if current_path and current_path != object:
		current_path.disconnect("curve_changed", self, "_on_point_selector_item_selected")
	current_path = object
	current_path.connect("curve_changed", self, "_on_point_selector_item_selected")
	if not current_path:
		return
	if not current_path.curve:
		return

	var num_points = current_path.curve.get_point_count()

	point_index.clear()
	for i in range(num_points):
		point_index.add_item("Point %s" % i)

	# Update UI box, which will also create/update the backup_curve.
	_on_edit_focus_exited()


# Propogate an edit from the panel over to the curve itself.
func update_path_point():
	var loc = Vector3(
		float(edit_x.text),
		float(edit_y.text),
		float(edit_z.text))

	var index = point_index.selected
	if point_part.selected == POS:
		current_path.curve.set_point_position(index, loc)
	elif point_part.selected == IN:
		current_path.curve.set_point_in(index, loc)
	elif point_part.selected == OUT:
		current_path.curve.set_point_out(index, loc)

	# Save a copy of the current curve so we can later detect any handle changes.
	backup_curve = current_path.curve.duplicate()
	backup_path = weakref(current_path)


# Called by all x, y, and z line edits after focus left or enter pressed.
func _on_edit_focus_exited():
	if _internal_update:
		return

	_internal_update = true
	_enforce_numeric_values(edit_x.text, edit_x)
	_enforce_numeric_values(edit_y.text, edit_y)
	_enforce_numeric_values(edit_z.text, edit_z)
	_internal_update = false
	update_path_point()


func _enforce_numeric_values(new_text:String, line_edit:LineEdit):
	var init_cursor_pos = line_edit.caret_position
	var compiled = ''
	var regex = RegEx.new()
	regex.compile("[-+]?[0-9]*\\.?[0-9]+")
	for valid_character in regex.search_all(str(new_text)):
		compiled += valid_character.get_string()
	line_edit.set_text(compiled)
	line_edit.caret_position = init_cursor_pos


# When the option dropdown is modified. No actual curve changes to apply.
func _on_point_selector_item_selected(_index=0):
	if not current_path:
		return
	if not current_path.curve:
		return
	var num_points = current_path.curve.get_point_count()

	# Check the backed up curve against the current curve.
	if (
		backup_path != null
		and current_path == backup_path.get_ref()
		and backup_curve.get_point_count() == num_points
	):
		var something_changed = false
		for pt in range(num_points):
			if backup_curve.get_point_position(pt) != current_path.curve.get_point_position(pt):
				point_part.selected = POS
				point_index.selected = pt
				break
			if backup_curve.get_point_in(pt) != current_path.curve.get_point_in(pt):
				point_part.selected = IN
				point_index.selected = pt
				break
			if backup_curve.get_point_out(pt) != current_path.curve.get_point_out(pt):
				point_part.selected = OUT
				point_index.selected = pt
				break

	var pt
	if point_part.selected == POS:
		pt = current_path.curve.get_point_position(point_index.selected)
	elif point_part.selected == IN:
		pt = current_path.curve.get_point_in(point_index.selected)
	elif point_part.selected == OUT:
		pt = current_path.curve.get_point_out(point_index.selected)

	_internal_update = true
	_enforce_numeric_values(str(pt.x), edit_x)
	_enforce_numeric_values(str(pt.y), edit_y)
	_enforce_numeric_values(str(pt.z), edit_z)
	_internal_update = false

	# Update the back, since the transform active selection already changed.
	backup_curve = current_path.curve.duplicate()
	backup_path = weakref(current_path)


func _on_x_edit_focus_entered():
	edit_x.select_all()


func _on_y_edit_focus_entered():
	edit_y.select_all()


func _on_z_edit_focus_entered():
	edit_z.select_all()


func _on_x_edit_gui_input(event):
	if event is InputEventMouseButton and event.button_index == BUTTON_LEFT:
		edit_x.select_all()


func _on_y_edit_gui_input(event):
	if event is InputEventMouseButton and event.button_index == BUTTON_LEFT:
		edit_y.select_all()


func _on_z_edit_gui_input(event):
	if event is InputEventMouseButton and event.button_index == BUTTON_LEFT:
		edit_z.select_all()
