shader_type canvas_item;

uniform float border_width : hint_range(0f, 10f);
uniform vec4 border_color : hint_color = vec4(1);

uniform float SAMPLES = 11.0;
const float WIDTH = 0.04734573810584494679397346954847;

uniform vec2 blur_scale = vec2(1, 0);

const vec2 OFFSETS[8] = {
	vec2(-1, -1), vec2(-1, 0), vec2(-1, 1), vec2(0, -1), vec2(0, 1), 
	vec2(1, -1), vec2(1, 0), vec2(1, 1)
};

vec4 sample(sampler2D tex, vec2 uv, vec2 offset)
{
	//Because of vertex scaling, scale texture to 1/2 size
	vec2 resized_uv = (uv - vec2(.5f, .5f) - offset) * 2f;
	vec4 color = texture(tex, resized_uv + vec2(.5f, .5f));

	vec2 mask_vec = step(uv, vec2(.25f) + offset);
	mask_vec += step(vec2(.75f) + offset, uv);
	float mask = clamp(mask_vec.x + mask_vec.y, 0f, 1f);
	
	color.rgb = mix(color.rgb, vec3(1f), mask);
	color.a = mix(1f, 0f, mask);
	return color;
}

float gaussian(float x) {
    return WIDTH * exp((x * x / (2.0 * SAMPLES)) * -1.0);
}

void fragment()
{
	vec2 offset = vec2(0f) * TEXTURE_PIXEL_SIZE; //Optional position offset
	vec4 color = sample(TEXTURE, UV, offset);
	
	vec2 size = TEXTURE_PIXEL_SIZE * border_width;
	
	float outline = 0f;
	for (int i = 0; i < 8; i++)
	{
		outline += sample(TEXTURE, UV + size * OFFSETS[i], offset).a;
	}
	
	vec2 mask_vec = step(UV, vec2(.25f) + offset);
	mask_vec += step(vec2(.75f) + offset, UV);
	float mask = clamp(mask_vec.x + mask_vec.y, 0f, 1f);
	outline = min(outline, 1.0);
	
	COLOR = mix(color, border_color, outline - color.a);
	
	//Blur
	vec2 scale = TEXTURE_PIXEL_SIZE * blur_scale;
	
	float weight = 0.0;
	float total_weight = 0.0;
	color = vec4(0.0);
	
	for(int i=-int(SAMPLES)/2; i < int(SAMPLES)/2; ++i) {
		weight = gaussian(float(i));
		color.rgb += texture(SCREEN_TEXTURE, SCREEN_UV + scale * vec2(float(i))).rgb * weight;
		total_weight += weight;
	}
	
	vec4 finalColor = color / total_weight;
	COLOR = mix(COLOR, finalColor, mask);
}

void vertex()
{
	 //Give room around the side for effects
	VERTEX *= 2f;
}
