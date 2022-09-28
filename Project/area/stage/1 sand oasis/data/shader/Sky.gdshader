shader_type sky;
render_mode use_debanding;

uniform vec4 sky_top_color : source_color = vec4(0.385, 0.454, 0.55, 1.0);
uniform vec4 sky_horizon_color : source_color = vec4(0.646, 0.656, 0.67, 1.0);
uniform float sky_curve : hint_range(0, 1) = 0.15;
uniform float sky_energy = 1.0; // In Lux.
uniform sampler2D sky_cover : source_color, hint_default_black;
uniform vec4 sky_cover_modulate : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 ground_bottom_color : source_color = vec4(0.2, 0.169, 0.133, 1.0);
uniform vec4 ground_horizon_color : source_color = vec4(0.646, 0.656, 0.67, 1.0);
uniform float ground_curve : hint_range(0, 1) = 0.02;
uniform float ground_energy = 1.0;
uniform float sun_angle_max = 30.0;
uniform float sun_curve : hint_range(0, 1) = 0.15;

void sky() {
	float v_angle = acos(clamp(EYEDIR.y, -1.0, 1.0));
	float c = (1.0 - v_angle / (PI * 0.5));
	vec3 sky = mix(sky_horizon_color.rgb, sky_top_color.rgb, clamp(1.0 - pow(1.0 - c, 1.0 / sky_curve), 0.0, 1.0));
	sky *= sky_energy;

	if (LIGHT0_ENABLED) {
		float sun_angle = acos(dot(LIGHT0_DIRECTION, EYEDIR));
		if (sun_angle < LIGHT0_SIZE) {
			sky = LIGHT0_COLOR * LIGHT0_ENERGY;
		} else if (sun_angle < sun_angle_max) {
			float c2 = (sun_angle - LIGHT0_SIZE) / (sun_angle_max - LIGHT0_SIZE);
			sky = mix(LIGHT0_COLOR * LIGHT0_ENERGY, sky, clamp(1.0 - pow(1.0 - c2, 1.0 / sun_curve), 0.0, 1.0));
		}
	}

	if (LIGHT1_ENABLED) {
		float sun_angle = acos(dot(LIGHT1_DIRECTION, EYEDIR));
		if (sun_angle < LIGHT1_SIZE) {
			sky = LIGHT1_COLOR * LIGHT1_ENERGY;
		} else if (sun_angle < sun_angle_max) {
			float c2 = (sun_angle - LIGHT1_SIZE) / (sun_angle_max - LIGHT1_SIZE);
			sky = mix(LIGHT1_COLOR * LIGHT1_ENERGY, sky, clamp(1.0 - pow(1.0 - c2, 1.0 / sun_curve), 0.0, 1.0));
		}
	}

	if (LIGHT2_ENABLED) {
		float sun_angle = acos(dot(LIGHT2_DIRECTION, EYEDIR));
		if (sun_angle < LIGHT2_SIZE) {
			sky = LIGHT2_COLOR * LIGHT2_ENERGY;
		} else if (sun_angle < sun_angle_max) {
			float c2 = (sun_angle - LIGHT2_SIZE) / (sun_angle_max - LIGHT2_SIZE);
			sky = mix(LIGHT2_COLOR * LIGHT2_ENERGY, sky, clamp(1.0 - pow(1.0 - c2, 1.0 / sun_curve), 0.0, 1.0));
		}
	}

	if (LIGHT3_ENABLED) {
		float sun_angle = acos(dot(LIGHT3_DIRECTION, EYEDIR));
		if (sun_angle < LIGHT3_SIZE) {
			sky = LIGHT3_COLOR * LIGHT3_ENERGY;
		} else if (sun_angle < sun_angle_max) {
			float c2 = (sun_angle - LIGHT3_SIZE) / (sun_angle_max - LIGHT3_SIZE);
			sky = mix(LIGHT3_COLOR * LIGHT3_ENERGY, sky, clamp(1.0 - pow(1.0 - c2, 1.0 / sun_curve), 0.0, 1.0));
		}
	}

	vec4 sky_cover_texture = texture(sky_cover, SKY_COORDS);
	sky += (sky_cover_texture.rgb * sky_cover_modulate.rgb) * sky_cover_texture.a * sky_cover_modulate.a * sky_energy;

	c = (v_angle - (PI * 0.5)) / (PI * 0.5);
	vec3 ground = mix(ground_horizon_color.rgb, ground_bottom_color.rgb, clamp(1.0 - pow(1.0 - c, 1.0 / ground_curve), 0.0, 1.0));
	ground *= ground_energy;

	COLOR = mix(ground, sky, step(0.0, EYEDIR.y));
}
