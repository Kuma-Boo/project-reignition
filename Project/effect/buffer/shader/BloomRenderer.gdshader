shader_type spatial;
render_mode unshaded, cull_front, depth_draw_never, depth_test_disabled;

uniform sampler2D depth_texture : source_color;
uniform float far_clip_distance;
uniform bool multiply;

void vertex()
{
    POSITION = vec4(VERTEX, 1.0);
}

void fragment()
{
	if(multiply)
		ALBEDO = vec3(1.0);
	else
		ALBEDO = vec3(0.0);
		
	float depth = texture(DEPTH_TEXTURE, SCREEN_UV).x;
	vec3 ndc = vec3(SCREEN_UV, depth) * 2.0 - 1.0;
	vec4 view = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
	float linear_depth = -(view.xyz / view.w).z;
	float normalized_depth = linear_depth / far_clip_distance;
	float geometry_depth = texture(depth_texture, SCREEN_UV).r;
	
	if(normalized_depth + (far_clip_distance * .00001) < 1.0)
	{
		//Has clipping issues due to subviewports rendering in ldr
		if(geometry_depth > normalized_depth)
		{
			vec4 col = texture(SCREEN_TEXTURE, SCREEN_UV);
			ALBEDO = mix(ALBEDO, col.rgb, col.a);
		}
	}
}