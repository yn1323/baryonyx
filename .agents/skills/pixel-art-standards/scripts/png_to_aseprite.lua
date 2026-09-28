-- 1ドット1ピクセルのPNGを、0番を透明・1番以降を暗い順に並べたパレットのインデックスカラーの .aseprite に保存する。
--   Aseprite.exe -b --script-param src=IN.png --script-param ase=OUT.aseprite --script png_to_aseprite.lua
local pc = app.pixelColor
local spr = app.open(app.params["src"])
local img = Image(spr.spec)
img:drawImage(spr.cels[1].image, spr.cels[1].position)

local palette, seen = {}, {}
for it in img:pixels() do
  local v = it()
  if pc.rgbaA(v) == 255 then
    local key = v & 0xFFFFFF
    if not seen[key] then
      seen[key] = true
      palette[#palette + 1] = { pc.rgbaR(v), pc.rgbaG(v), pc.rgbaB(v) }
    end
  end
end
local function lum(c) return 0.299 * c[1] + 0.587 * c[2] + 0.114 * c[3] end
table.sort(palette, function(a, b) return lum(a) < lum(b) end)

app.transaction("To indexed", function()
  spr.cels[1].image = img
  spr.cels[1].position = Point(0, 0)
  local pal = Palette(#palette + 1)
  pal:setColor(0, Color { r = 0, g = 0, b = 0, a = 0 })
  for i, c in ipairs(palette) do pal:setColor(i, Color { r = c[1], g = c[2], b = c[3], a = 255 }) end
  spr:setPalette(pal)
  app.command.ChangePixelFormat { format = "indexed", dithering = "none" }
  spr.transparentColor = 0
end)
spr:saveAs(app.params["ase"])
spr:close()
