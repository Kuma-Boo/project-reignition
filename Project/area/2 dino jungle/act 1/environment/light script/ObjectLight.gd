extends DirectionalLight3D

var targetBrightness = 0.7


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta):
	if abs(light_energy - targetBrightness) < 0.01:
		light_energy = targetBrightness
	else:
		light_energy = lerp(light_energy, targetBrightness, 0.3 * delta * 60)
	



func _on_cavern_enter_area_entered(area):
	targetBrightness = 0.0


func _on_cavern_exit_area_entered(area):
	targetBrightness = 0.7
