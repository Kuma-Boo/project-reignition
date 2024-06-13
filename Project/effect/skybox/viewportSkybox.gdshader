shader_type sky;

uniform sampler2D viewport : source_color;

void sky() {
	COLOR = texture(viewport, SCREEN_UV).rgb;
}
