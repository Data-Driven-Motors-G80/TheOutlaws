// Draws the level-card heat peppers:
//   Assets/Resources/WorldShiftChili.png       lit pepper (red body, green cap, dark outline so it reads on any art)
//   Assets/Resources/WorldShiftChiliEmpty.png  unlit pepper (same silhouette, solid white, tinted faintly at runtime)
// Run from the repository root: swift Tools/Art/generate_chili_icon.swift
import AppKit
import CoreGraphics

let size = 192
func rgb(_ hex: UInt32, _ a: CGFloat = 1) -> CGColor {
    CGColor(srgbRed: CGFloat((hex >> 16) & 255) / 255, green: CGFloat((hex >> 8) & 255) / 255, blue: CGFloat(hex & 255) / 255, alpha: a)
}
func p(_ x: CGFloat, _ y: CGFloat) -> CGPoint { CGPoint(x: x * 1.5, y: y * 1.5) } // authored on a 128 grid

// Chubby, curved body with a hooked tip.
let body = CGMutablePath()
body.move(to: p(26, 44))
body.addCurve(to: p(112, 118), control1: p(14, 92), control2: p(62, 124))
body.addCurve(to: p(70, 46), control1: p(88, 98), control2: p(76, 76))
body.addCurve(to: p(26, 44), control1: p(62, 30), control2: p(34, 30))
body.closeSubpath()
let cap = CGMutablePath()
cap.move(to: p(18, 44)); cap.addCurve(to: p(76, 46), control1: p(26, 24), control2: p(66, 22))
cap.addCurve(to: p(48, 50), control1: p(68, 56), control2: p(58, 48))
cap.addCurve(to: p(18, 44), control1: p(38, 56), control2: p(26, 56))
cap.closeSubpath()
let stem = CGMutablePath()
stem.move(to: p(46, 36)); stem.addCurve(to: p(64, 10), control1: p(42, 18), control2: p(52, 8))
stem.addCurve(to: p(72, 18), control1: p(70, 10), control2: p(74, 14))

func canvas() -> CGContext {
    let ctx = CGContext(data: nil, width: size, height: size, bitsPerComponent: 8, bytesPerRow: 0,
                        space: CGColorSpace(name: CGColorSpace.sRGB)!, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    ctx.translateBy(x: 0, y: CGFloat(size)); ctx.scaleBy(x: 1, y: -1)
    ctx.setLineJoin(.round); ctx.setLineCap(.round)
    return ctx
}
func save(_ ctx: CGContext, _ path: String) {
    guard let image = ctx.makeImage(), let png = NSBitmapImageRep(cgImage: image).representation(using: .png, properties: [:]) else { fatalError("encode") }
    try! png.write(to: URL(fileURLWithPath: path)); print("Wrote \(path)")
}

// Lit pepper.
let lit = canvas()
let ink = rgb(0x2A0B08)
for (shape, width) in [(body as CGPath, 13.0), (cap as CGPath, 13.0)] { lit.addPath(shape); lit.setStrokeColor(ink); lit.setLineWidth(width); lit.strokePath() }
lit.addPath(stem); lit.setStrokeColor(ink); lit.setLineWidth(20); lit.strokePath()
lit.addPath(body); lit.setFillColor(rgb(0xF0432F)); lit.fillPath()
lit.saveGState(); lit.addPath(body); lit.clip()
let shade = CGMutablePath()
shade.move(to: p(20, 70)); shade.addCurve(to: p(118, 122), control1: p(30, 108), control2: p(84, 128))
shade.addLine(to: p(0, 128)); shade.closeSubpath()
lit.addPath(shade); lit.setFillColor(rgb(0xB0201A, 0.6)); lit.fillPath(); lit.restoreGState()
lit.setStrokeColor(rgb(0xFFFFFF, 0.7)); lit.setLineWidth(9)
lit.move(to: p(40, 54)); lit.addCurve(to: p(70, 96), control1: p(40, 74), control2: p(52, 90)); lit.strokePath()
lit.addPath(cap); lit.setFillColor(rgb(0x4CB85A)); lit.fillPath()
lit.addPath(stem); lit.setStrokeColor(rgb(0x2F8A3E)); lit.setLineWidth(10); lit.strokePath()
save(lit, "Assets/Resources/WorldShiftChili.png")

// Unlit pepper: one clean white silhouette (body, cap and stem), tinted faintly at runtime.
let empty = canvas()
for shape in [body as CGPath, cap as CGPath] { empty.addPath(shape); empty.setStrokeColor(rgb(0xFFFFFF)); empty.setLineWidth(13); empty.strokePath() }
empty.addPath(stem); empty.setStrokeColor(rgb(0xFFFFFF)); empty.setLineWidth(20); empty.strokePath()
for shape in [body as CGPath, cap as CGPath] { empty.addPath(shape); empty.setFillColor(rgb(0xFFFFFF)); empty.fillPath() }
save(empty, "Assets/Resources/WorldShiftChiliEmpty.png")
