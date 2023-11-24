shader_type spatial;
render_mode unshaded, cull_front, depth_draw_never, depth_test_disabled;

// Depth texture fed from the game view
uniform sampler2D depth_texture : source_color, filter_linear_mipmap;
uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;

// Depth texture from the bloom viewport
uniform sampler2D bloom_depth_texture : hint_depth_texture, filter_linear_mipmap;
global uniform float render_distance;

void vertex()
{
    POSITION = vec4(VERTEX, 1.0);
}

void fragment()
{
	float alpha = texture(screen_texture, SCREEN_UV).a;
	if(abs(alpha) < 0.01) // Turn transparent pixels to mid-gray
	{
		ALBEDO = vec3(.5);
		ALPHA = 1.0;
	}
	else
	{
		float depth = texture(depth_texture, SCREEN_UV).z;
		float bloom_depth = texture(bloom_depth_texture, SCREEN_UV).x;
		vec3 ndc = vec3(SCREEN_UV, bloom_depth) * 2.0 - 1.0;
		vec4 view = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
		float linear_depth = -(view.xyz / view.w).z;
		float normalized_depth = linear_depth / render_distance;
		
		//ALBEDO = texture(screen_texture, SCREEN_UV).rgb;

		if(depth > bloom_depth) // Covered by gameplay
			ALBEDO = vec3(0);
		else
			ALBEDO = vec3(1.0);
	}
}