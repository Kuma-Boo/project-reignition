shader_type particles;
render_mode disable_velocity;
render_mode collision_use_scale;

uniform vec3 direction;
uniform float water_speed;
uniform vec3 emission_shape_offset = vec3(0.0);
uniform vec3 emission_box_extents;
uniform sampler2D alpha_curve : repeat_disable;

float rand_from_seed(inout uint seed) {
	int k;
	int s = int(seed);
	if (s == 0) {
		s = 305420679;
	}
	k = s / 127773;
	s = 16807 * (s - k * 127773) - 2836 * k;
	if (s < 0) {
		s += 2147483647;
	}
	seed = uint(s);
	return float(seed % uint(65536)) / 65535.0;
}

uint hash(uint x) {
	x = ((x >> uint(16)) ^ x) * uint(73244475);
	x = ((x >> uint(16)) ^ x) * uint(73244475);
	x = (x >> uint(16)) ^ x;
	return x;
}

struct DisplayParameters {
	vec3 scale;
	float lifetime;
	float alpha;
};

void calculate_initial_display_params(inout DisplayParameters params, inout uint alt_seed) {
	params.scale = vec3(1.0);
	params.lifetime = 1.0;
	params.alpha = 1.0;
}

vec3 calculate_initial_position(inout DisplayParameters params, inout uint alt_seed) {
	vec3 pos = vec3(0.0);
	{ // Emission shape.
		pos = vec3(rand_from_seed(alt_seed) * 2.0 - 1.0, rand_from_seed(alt_seed) * 2.0 - 1.0, rand_from_seed(alt_seed) * 2.0 - 1.0) * emission_box_extents;
	}
	return pos + emission_shape_offset;
}

void process_display_param(inout DisplayParameters parameters, float lifetime) {
	parameters.alpha *= texture(alpha_curve, vec2(lifetime)).r;
}

void start() {
	uint alt_seed = hash(NUMBER + uint(1) + RANDOM_SEED);
	DisplayParameters params;
	calculate_initial_display_params(params, alt_seed);
	process_display_param(params, 0.0);
	if (rand_from_seed(alt_seed) > AMOUNT_RATIO) {
		ACTIVE = false;
	}

	if (RESTART_CUSTOM) {
		CUSTOM = vec4(0.0);
		CUSTOM.w = params.lifetime;
	}
	if (RESTART_ROT_SCALE) {
		TRANSFORM[0].xyz = vec3(1.0, 0.0, 0.0);
		TRANSFORM[1].xyz = vec3(0.0, 1.0, 0.0);
		TRANSFORM[2].xyz = vec3(0.0, 0.0, 1.0);
	}
	if (RESTART_POSITION) {
		TRANSFORM[3].xyz = calculate_initial_position(params, alt_seed);
		TRANSFORM = EMISSION_TRANSFORM * TRANSFORM;
	}
	if (RESTART_VELOCITY) {
		VELOCITY = normalize(direction) * water_speed;
	}

	VELOCITY = (EMISSION_TRANSFORM * vec4(VELOCITY, 0.0)).xyz;
}

void process() {
	uint alt_seed = hash(NUMBER + uint(1) + RANDOM_SEED);
	DisplayParameters params;
	calculate_initial_display_params(params, alt_seed);

	float lifetime_percent = CUSTOM.y / params.lifetime;
	CUSTOM.y += DELTA / LIFETIME;
	CUSTOM.y = mix(CUSTOM.y, 1.0, INTERPOLATE_TO_END);
	if (CUSTOM.y > CUSTOM.w) {
		ACTIVE = false;
	}

	if (COLLIDED) {
		TRANSFORM[3].xyz += COLLISION_NORMAL * COLLISION_DEPTH;
		VELOCITY = vec3(0.0);
	}

	TRANSFORM[3].xyz += VELOCITY * DELTA;
	process_display_param(params, lifetime_percent);
	COLOR.a = params.alpha;
	
	if (COLLIDED) {
		mat4 transform = TRANSFORM;
		transform[0].xyz = cross(vec3(0.0, 0.0, 1.0), -COLLISION_NORMAL);
		transform[1].xyz = normalize(-COLLISION_NORMAL);
		transform[2].xyz = normalize(cross(transform[0].xyz, transform[1].xyz));

		uint flags = FLAG_EMIT_POSITION | FLAG_EMIT_ROT_SCALE;
		emit_subparticle(transform, VELOCITY, vec4(0.0), vec4(0.0), flags);
	}

	if (CUSTOM.y > CUSTOM.w) {
		ACTIVE = false;
	}
}
