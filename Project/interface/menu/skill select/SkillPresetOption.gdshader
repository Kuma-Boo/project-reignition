shader_type canvas_item;

uniform sampler2D vertical_gradient; // used to set alpha values. So you can use this to make the effect dis-/re-appear at points
uniform sampler2D color_gradient; // sets the effect color, supports 2d gradients
uniform float size_effect: hint_range(-5.0, 5.0, 0.05) = 0.1; // size of the effect
uniform float speed = 1; // effect speed; Will reverse effect if set to negative
uniform float highlight_strength: hint_range(-4.0, 4.0, 0.05) = 0.5; // how strong the color overides the base color
uniform bool color_mode_toggle = false; // changes how the color gradient is applied. Default is on the entire texture, if set to true it is only applied to the effect
uniform bool is_horizontal = false; // changes direction from vertical to horizontal
uniform float pause_duration = 0.5; // duration of the pause between highlight cycles

void fragment() {
	// set up base parameters
	vec4 old_color = COLOR;
	float time = TIME * abs(speed); // Absolute time to handle both positive and negative speeds
	float effect_cycle_duration = 1.0 + pause_duration; // total time for one cycle and pause
	float mod_time = mod(time, effect_cycle_duration); // current time within the cycle

	// Handle the pause
	float progress = mod_time / (1.0); // Normalize mod_time to the active highlight duration only (excluding pause)

	// Ensure the effect is clamped during the pause period
	if (mod_time > 1.0) {
		progress = 1.0; // Hold the highlight at the final position during the pause
	}

	// Reverse the direction if speed is negative
	if (speed < 0.0) {
		progress = 1.0 - progress; // Reverse progress when speed is negative
	}

	// Set the current time and bounds for the highlight effect
	float current_time = mix(0.0 - size_effect, 1.0 + size_effect, progress);
	float effect_lower_bound = current_time - size_effect;
	float effect_upper_bound = current_time + size_effect;
	float position_value = (is_horizontal) ? UV.x : UV.y;

	// Smooth out the lower/upper bounds with the UV values to calculate how far from the middle point (the effect) the pixel is
	float effect_distance = smoothstep(effect_lower_bound, current_time, position_value) - smoothstep(current_time, effect_upper_bound, position_value);

	// Get the position of the pixel within the effect for the inner_gradient color mode
	float inner_effect_position = smoothstep(effect_lower_bound, effect_upper_bound, position_value);
	vec2 color_position = (color_mode_toggle) ? vec2(UV.x, inner_effect_position) : vec2(progress);

	// Get the new color from the gradient
	vec4 new_color = vec4(texture(color_gradient, color_position));

	// Calculate the vertical gradient and alpha_values
	new_color = mix(old_color, new_color, vec4(texture(vertical_gradient, vec2(progress))));

	// Apply the color to the texture
	COLOR.rgb = mix(old_color.rgb, new_color.rgb, vec3(effect_distance * highlight_strength));
}