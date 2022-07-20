shader_type canvas_item;

//TODO Figure out how to use the timebreak texture
uniform float effect : hint_range(0.0, 1.0);
uniform sampler2D timebreak_effect : hint_albedo;

void fragment()
{
	vec4 col = textureLod(SCREEN_TEXTURE, SCREEN_UV, 0.0);
	float grayscale = (max(col.r, max(col.g, col.b)) + min(col.r, min(col.g, col.b))) * .5;
	col.rgb = mix(col.rgb, vec3(grayscale), .75 * effect);
	COLOR = col;
}
