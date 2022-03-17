tool
extends BitmapFont

var index = 0
export(Vector2) var GlyphSize = Vector2(8,8) setget changeGlyphSize
export(Texture) var TextureToCut = null setget changeTexture
export(String) var CharSet = "0123456789" setget changeCharSet
export(float) var Spacing = 1 setget changeSpacing

func changeCharSet(value):
	CharSet = value
	update()

func changeGlyphSize(value):
	GlyphSize = value
	height = GlyphSize.y
	update()

func changeTexture(value):
	TextureToCut = value
	index = 0
	if TextureToCut:
		update()

func changeSpacing(value):
	Spacing = value
	update()

func update():
	print("Cut texture to font")
	if TextureToCut != null:
		if GlyphSize.x > 0 and GlyphSize.y > 0:

			var w  = TextureToCut.get_width()
			var h  = TextureToCut.get_height()
			var tx = w / GlyphSize.x
			var ty = h / GlyphSize.y

			var font = self
			var i = 0  #Iterator for char index

			clear()


			#Begin cutting..... so edgy
			font.add_texture(TextureToCut)
			font.height = GlyphSize.y

			for ch in CharSet:
				var region = Rect2(Vector2((i % int(tx)) * GlyphSize.x, (int(i / tx)) * GlyphSize.y), GlyphSize)
				var unicode = ord(ch)
				font.add_char(unicode, 0, region, Vector2.ZERO,  GlyphSize.y + Spacing)
				i += 1

			update_changes()
	pass #if texture is null
