shader_type particles;
//Lifetime must be >= 1!

uniform float height = 5.0;
uniform float rotation_bias;
uniform float rotation_speed = 1.0;
uniform float radius_randomness : hint_range(0.0, 1.0, 0.1);
uniform float radius_randomness_bias : hint_range(-1.0, 1.0, 0.1);
uniform sampler2D radius_curve : repeat_disable;
uniform sampler2D scale_curve : repeat_disable;
uniform sampler2D color_ramp : repeat_disable;

float rand_from_seed(inout uint seed) {
	int k;
	int s = int(seed);
	if (s == 0)
	s = 305420679;
	k = s / 127773;
	s = 16807 * (s - k * 127773) - 2836 * k;
	if (s < 0)
		s += 2147483647;
	seed = uint(s);
	return float(seed % uint(65536)) / 65535.0;
}

float rand_from_seed_m1_p1(inout uint seed) {
	return rand_from_seed(seed) * 2.0 - 1.0;
}

uint hash(uint x) {
	x = ((x >> uint(16)) ^ x) * uint(73244475);
	x = ((x >> uint(16)) ^ x) * uint(73244475);
	x = (x >> uint(16)) ^ x;
	return x;
}

vec2 calculate_rotation(float rotation_factor)
{
	float rotation_amount = PI * 2.0 * rotation_factor + rotation_bias; //Rotation amount, in radians
	float cosine = cos(rotation_amount);
	float sine = sin(rotation_amount);
	
	mat2 rotation_matrix = mat2(vec2(cosine, sine), vec2(cosine, -sine));
	return rotation_matrix * vec2(1, 0);
}

void process()
{
	CUSTOM.y += DELTA / LIFETIME;
	
	if (RESTART)
	{
		VELOCITY = vec3(0, 0, 0);
		
		//Reset transform
		TRANSFORM[0].xyz = vec3(1, 0, 0);
		TRANSFORM[1].xyz = vec3(0, 1, 0);
		TRANSFORM[2].xyz = vec3(1, 0, 1);
		
		CUSTOM.y = 0.0; //Reset lifetime
		uint alt_seed = hash(NUMBER + uint(1) + RANDOM_SEED);
		CUSTOM.z = mix(0.0, 1.0, rand_from_seed(alt_seed)); //Randomize animation frame
		
		TRANSFORM[0].w = (mix(0.0, 1.0, rand_from_seed_m1_p1(alt_seed)) * radius_randomness) + radius_randomness_bias; //Cache random distance
	}
	
	float particle_lifetime = CUSTOM.y;
	
	float radius = textureLod(radius_curve, vec2(particle_lifetime, 0), 0.0f).x;
	TRANSFORM[3].xz = calculate_rotation(particle_lifetime * rotation_speed) * (radius + TRANSFORM[0].w);
	TRANSFORM[3].y = mix(height, 0, particle_lifetime) + TRANSFORM[0].w; //Interpolate height
	
	TRANSFORM[0].xyz = normalize(TRANSFORM[0].xyz);
	TRANSFORM[1].xyz = normalize(TRANSFORM[1].xyz);
	TRANSFORM[2].xyz = normalize(TRANSFORM[2].xyz);
	
	//Apply scale
	vec3 tex_scale = textureLod(scale_curve, vec2(particle_lifetime, 0), 0.0f).xyz;

	TRANSFORM[0].xyz *= sign(tex_scale.x) * max(abs(tex_scale.x), 0.001);
	TRANSFORM[1].xyz *= sign(tex_scale.y) * max(abs(tex_scale.y), 0.001);
	TRANSFORM[2].xyz *= sign(tex_scale.z) * max(abs(tex_scale.z), 0.001);
	
	//Update color
	COLOR = texture(color_ramp, vec2(particle_lifetime, 0));
	if (CUSTOM.y > LIFETIME)
		ACTIVE = false;
}
