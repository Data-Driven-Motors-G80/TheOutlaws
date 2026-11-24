// Repaints City shop atlas tiles 3 and 4 (top row) with family-friendly shops:
// tile 3 = ICE CREAM (flat cone on cream), tile 4 = BAKERY (neon cupcake on black).
// Run from the repository root: swift Tools/Art/replace_city_shop_tiles.swift
import AppKit
import CoreGraphics

let path = "Assets/Resources/WorldShiftCityShops.png"
guard let source = NSImage(contentsOfFile: path),
      let original = source.cgImage(forProposedRect: nil, context: nil, hints: nil) else { fatalError("Cannot read \(path)") }
let W = original.width, H = original.height
let ctx = CGContext(data: nil, width: W, height: H, bitsPerComponent: 8, bytesPerRow: 0,
                    space: CGColorSpace(name: CGColorSpace.sRGB)!, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
ctx.draw(original, in: CGRect(x: 0, y: 0, width: W, height: H))
// Work in top-left pixel coordinates, scaled from the 1254 px authoring size.
let k = CGFloat(W) / 1254
ctx.translateBy(x: 0, y: CGFloat(H)); ctx.scaleBy(x: k, y: -k)

func rgb(_ hex: UInt32, _ a: CGFloat = 1) -> CGColor {
    CGColor(srgbRed: CGFloat((hex >> 16) & 255) / 255, green: CGFloat((hex >> 8) & 255) / 255, blue: CGFloat(hex & 255) / 255, alpha: a)
}
func fill(_ p: CGPath, _ c: CGColor) { ctx.addPath(p); ctx.setFillColor(c); ctx.fillPath() }
func circle(_ x: CGFloat, _ y: CGFloat, _ r: CGFloat) -> CGPath { CGPath(ellipseIn: CGRect(x: x - r, y: y - r, width: r * 2, height: r * 2), transform: nil) }

// ---- Tile 3: ICE CREAM ----
fill(CGPath(rect: CGRect(x: 629, y: 0, width: 310, height: 626), transform: nil), rgb(0xF6F0E2))
let cone = CGMutablePath(); cone.move(to: CGPoint(x: 712, y: 335)); cone.addLine(to: CGPoint(x: 856, y: 335)); cone.addLine(to: CGPoint(x: 784, y: 565)); cone.closeSubpath()
fill(cone, rgb(0xDDA55E))
ctx.saveGState(); ctx.addPath(cone); ctx.clip()
ctx.setStrokeColor(rgb(0xB07434)); ctx.setLineWidth(5)
for i in stride(from: -300, through: 300, by: 34) {
    let x = CGFloat(784 + i)
    ctx.move(to: CGPoint(x: x - 140, y: 335)); ctx.addLine(to: CGPoint(x: x + 140, y: 565))
    ctx.move(to: CGPoint(x: x + 140, y: 335)); ctx.addLine(to: CGPoint(x: x - 140, y: 565))
}
ctx.strokePath(); ctx.restoreGState()
// Mint scoop with drips over the cone rim, then a strawberry scoop.
let mint = rgb(0x9FDCBC)
fill(circle(784, 290, 86), mint)
for (x, r): (CGFloat, CGFloat) in [(716, 26), (752, 30), (790, 27), (826, 31), (856, 24)] { fill(circle(x, 338, r), mint) }
fill(circle(784, 180, 74), rgb(0xF4A0BA))
for (x, r): (CGFloat, CGFloat) in [(728, 22), (762, 25), (800, 24), (838, 21)] { fill(circle(x, 232, r), rgb(0xF4A0BA)) }
// Highlights and sprinkles.
fill(CGPath(ellipseIn: CGRect(x: 732, y: 140, width: 34, height: 22), transform: nil), rgb(0xFFFFFF, 0.45))
fill(CGPath(ellipseIn: CGRect(x: 726, y: 252, width: 38, height: 22), transform: nil), rgb(0xFFFFFF, 0.4))
let sprinkles: [(CGFloat, CGFloat, CGFloat, UInt32)] = [(760, 175, 0.6, 0xFFD54A), (800, 150, -0.5, 0x4FA3E0), (820, 200, 0.9, 0xFFFFFF),
    (748, 210, -0.8, 0x7BC96F), (790, 210, 0.2, 0xFFD54A), (826, 168, 1.3, 0x7BC96F), (776, 130, -1.1, 0xFFFFFF)]
for (x, y, a, c) in sprinkles {
    ctx.saveGState(); ctx.translateBy(x: x, y: y); ctx.rotate(by: a)
    fill(CGPath(roundedRect: CGRect(x: -10, y: -3.5, width: 20, height: 7), cornerWidth: 3.5, cornerHeight: 3.5, transform: nil), rgb(c))
    ctx.restoreGState()
}
ctx.setStrokeColor(rgb(0x4E7A2E)); ctx.setLineWidth(5); ctx.setLineCap(.round)
ctx.move(to: CGPoint(x: 790, y: 96)); ctx.addQuadCurve(to: CGPoint(x: 812, y: 66), control: CGPoint(x: 794, y: 76)); ctx.strokePath()
fill(circle(790, 106, 19), rgb(0xD8323C))
fill(circle(783, 99, 5), rgb(0xFFFFFF, 0.6))

// ---- Tile 4: BAKERY neon cupcake ----
fill(CGPath(rect: CGRect(x: 942, y: 0, width: 312, height: 626), transform: nil), rgb(0x000000))
ctx.translateBy(x: 0, y: -10)
func neon(_ p: CGPath, _ glow: UInt32, _ core: UInt32, fillBlack: Bool = true) {
    if fillBlack { fill(p, rgb(0x000000)) }
    ctx.saveGState(); ctx.setLineJoin(.round); ctx.setLineCap(.round)
    ctx.setShadow(offset: .zero, blur: 24, color: rgb(glow))
    ctx.addPath(p); ctx.setStrokeColor(rgb(glow)); ctx.setLineWidth(9); ctx.strokePath()
    ctx.addPath(p); ctx.setStrokeColor(rgb(glow)); ctx.setLineWidth(9); ctx.strokePath()
    ctx.setShadow(offset: .zero, blur: 0, color: nil)
    ctx.addPath(p); ctx.setStrokeColor(rgb(core)); ctx.setLineWidth(3); ctx.strokePath()
    ctx.restoreGState()
}
func capsule(_ x: CGFloat, _ y: CGFloat, _ w: CGFloat, _ h: CGFloat) -> CGPath {
    CGPath(roundedRect: CGRect(x: x, y: y, width: w, height: h), cornerWidth: h / 2, cornerHeight: h / 2, transform: nil)
}
let wrapper = CGMutablePath()
wrapper.move(to: CGPoint(x: 1022, y: 345)); wrapper.addLine(to: CGPoint(x: 1172, y: 345))
wrapper.addLine(to: CGPoint(x: 1149, y: 528)); wrapper.addLine(to: CGPoint(x: 1045, y: 528)); wrapper.closeSubpath()
for i in 1...5 {
    let t = CGFloat(i) / 6
    wrapper.move(to: CGPoint(x: 1022 + 150 * t, y: 352)); wrapper.addLine(to: CGPoint(x: 1045 + 104 * t, y: 520))
}
neon(wrapper, 0xFFA63D, 0xFFE2B0)
neon(capsule(1055, 196, 84, 54), 0xFF4FA3, 0xFFD3EA)
neon(capsule(1027, 240, 140, 58), 0xFF4FA3, 0xFFD3EA)
neon(capsule(1002, 290, 190, 60), 0xFF4FA3, 0xFFD3EA)
let stem = CGMutablePath(); stem.move(to: CGPoint(x: 1099, y: 164)); stem.addQuadCurve(to: CGPoint(x: 1122, y: 132), control: CGPoint(x: 1102, y: 142))
neon(stem, 0x63E06B, 0xD9FFD9, fillBlack: false)
neon(circle(1097, 180, 17), 0xFF3B3B, 0xFFC9C9)

guard let out = ctx.makeImage(), let png = NSBitmapImageRep(cgImage: out).representation(using: .png, properties: [:]) else { fatalError("encode") }
try! png.write(to: URL(fileURLWithPath: path))
print("Wrote \(path) \(W)x\(H)")
