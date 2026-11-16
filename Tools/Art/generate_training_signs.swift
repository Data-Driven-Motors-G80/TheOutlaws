// Original vector highway signs. Regenerate on macOS:
// swift -module-cache-path /tmp/outlaws-swift-cache Tools/Art/generate_training_signs.swift
import AppKit
import CoreText

let output = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
    .appendingPathComponent("Assets/Resources/WorldShiftTrainingSigns")
try FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)
// Use the same redistributable font files as the Unity UI, not platform defaults.
for name in ["Barlow-Medium.ttf","Barlow-Bold.ttf","BarlowCondensed-SemiBold.ttf"] {
    let url=URL(fileURLWithPath:FileManager.default.currentDirectoryPath).appendingPathComponent("Assets/Fonts/Barlow/"+name)
    CTFontManagerRegisterFontsForURL(url as CFURL,.process,nil)
}
func colour(_ hex: UInt32) -> NSColor {
    NSColor(calibratedRed: CGFloat((hex >> 16) & 255) / 255,
            green: CGFloat((hex >> 8) & 255) / 255, blue: CGFloat(hex & 255) / 255, alpha: 1)
}
let white = colour(0xF6F5E9), green = colour(0x075442), dark = colour(0x063B32)
let muted = colour(0xB9D6CB), yellow = colour(0xFFCC38), orange = colour(0xF48B32)
func box(_ x: CGFloat, _ y: CGFloat, _ w: CGFloat, _ h: CGFloat, _ fill: NSColor, _ radius: CGFloat = 0) {
    fill.setFill(); NSBezierPath(roundedRect: NSRect(x:x,y:y,width:w,height:h),xRadius:radius,yRadius:radius).fill()
}
func line(_ points: [(CGFloat,CGFloat)], _ fill: NSColor = white, _ width: CGFloat = 5) {
    let p = NSBezierPath(); p.move(to:NSPoint(x:points[0].0,y:points[0].1))
    for (x,y) in points.dropFirst() {p.line(to:NSPoint(x:x,y:y))}
    p.lineWidth=width;p.lineCapStyle = .round;p.lineJoinStyle = .round;fill.setStroke();p.stroke()
}
func text(_ value: String, _ x: CGFloat, _ y: CGFloat, _ size: CGFloat, _ fill: NSColor = white, _ bold: Bool = true, _ display: Bool = false) {
    let fontName = display ? "BarlowCondensed-SemiBold" : bold ? "Barlow-Bold" : "Barlow-Medium"
    let font = NSFont(name:fontName, size:size)!
    let attrs: [NSAttributedString.Key:Any] = [.font:font,.foregroundColor:fill]
    let s = value as NSString
    s.draw(at:NSPoint(x:x-s.size(withAttributes:attrs).width/2,y:y),withAttributes:attrs)
}
func arrow(_ x: CGFloat, _ y: CGFloat, _ direction: CGFloat = 1, _ fill: NSColor = white) {
    line([(x-34*direction,y),(x+34*direction,y)],fill,10)
    line([(x+12*direction,y-22),(x+34*direction,y),(x+12*direction,y+22)],fill,10)
}
func key(_ label: String, _ x: CGFloat, _ y: CGFloat, _ width: CGFloat = 110) {
    box(x-width/2,y-40,width,88,colour(0x042D28),14)
    box(x-width/2,y-44,width,84,white,14)
    text(label,x,y-(label.count > 2 ? 22 : 30),label.count > 2 ? 36 : 48,dark)
}
func movingBlock(_ x: CGFloat, _ y: CGFloat) {
    box(x-90,y-50,180,100,orange,6)
}
func polygon(_ points: [(CGFloat,CGFloat)], _ fill: NSColor) {
    let p=NSBezierPath();p.move(to:NSPoint(x:points[0].0,y:points[0].1))
    for (x,y) in points.dropFirst() {p.line(to:NSPoint(x:x,y:y))}
    p.close();fill.setFill();p.fill()
}
func disc(_ x: CGFloat, _ y: CGFloat, _ r: CGFloat, _ fill: NSColor) {
    fill.setFill();NSBezierPath(ovalIn:NSRect(x:x-r,y:y-r,width:r*2,height:r*2)).fill()
}
func world(_ x: CGFloat, _ y: CGFloat, _ night: Bool) {
    if night {
        disc(x,y,20,white);disc(x+10,y-7,18,green)
    } else {
        disc(x,y,13,yellow)
        for i in 0..<8 {
            let angle=CGFloat(i)*CGFloat.pi/4
            line([(x+cos(angle)*20,y+sin(angle)*20),(x+cos(angle)*27,y+sin(angle)*27)],yellow,5)
        }
    }
}
// Rear view matches the maroon coupe seen during play: tapered cabin, twin cream
// stripes, dark glazing, exposed tyres and red tail lamps. Never recolour it as traffic.
func playerCar(_ x: CGFloat, _ y: CGFloat, _ scale: CGFloat = 1) {
    NSGraphicsContext.saveGraphicsState()
    let transform=NSAffineTransform();transform.translateX(by:x,yBy:y);transform.scaleX(by:scale,yBy:scale);transform.concat()
    let paint=colour(0xA31438), lightPaint=colour(0xC53651), trim=colour(0x14242B), cream=colour(0xE3DEBA)
    box(-65,18,21,37,trim,5);box(44,18,21,37,trim,5)
    polygon([(-49,-19),(-35,-56),(35,-56),(49,-19)],paint)
    polygon([(-35,-53),(35,-53),(39,-44),(-39,-44)],lightPaint)
    box(-21,-54,10,10,cream);box(11,-54,10,10,cream)
    polygon([(-30,-42),(30,-42),(40,-23),(-40,-23)],colour(0x173C4A))
    line([(-28,-39),(26,-39)],colour(0x79A4AD),3)
    polygon([(-52,-21),(52,-21),(61,-2),(56,41),(-56,41),(-61,-2)],paint)
    polygon([(-52,-21),(52,-21),(58,-6),(-58,-6)],lightPaint)
    box(-21,-21,10,23,cream);box(11,-21,10,23,cream)
    line([(-51,3),(51,3)],colour(0x6B142F),3)
    box(-53,9,106,18,trim,3)
    box(-48,12,30,10,colour(0xFF5548),2);box(18,12,30,10,colour(0xFF5548),2)
    box(-11,25,22,9,cream,1);box(-55,36,110,8,trim,2)
    box(-58,-15,9,7,paint,2);box(49,-15,9,7,paint,2)
    NSGraphicsContext.restoreGraphicsState()
}
// A tall red van with white headlights faces the driver. The separate downward
// arrow conveys approaching traffic, rather than steering or a sideways mover.
func oncomingVan(_ x: CGFloat, _ y: CGFloat) {
    let trim=colour(0x14242B)
    box(x-58,y+22,19,32,trim,4);box(x+39,y+22,19,32,trim,4)
    polygon([(x-47,y-58),(x+47,y-58),(x+54,y-25),(x+58,y+37),(x-58,y+37),(x-54,y-25)],colour(0xDF2936))
    box(x-41,y-47,82,34,trim,4);line([(x,y-45),(x,y-15)],colour(0x718D98),3)
    polygon([(x-51,y-7),(x+51,y-7),(x+57,y+11),(x-57,y+11)],colour(0xF04B50))
    box(x-48,y+13,25,12,white,2);box(x+23,y+13,25,12,white,2)
    box(x-17,y+13,34,15,trim,2);box(x-54,y+34,108,9,trim,2)
}
// The Day barrier retracts flush with the road in Night: no floating dashed box.
func switchingBarrier(_ x: CGFloat, _ y: CGFloat, _ lowered: Bool) {
    if lowered {
        polygon([(x-76,y-51),(x+76,y-51),(x+112,y+65),(x-112,y+65)],colour(0x34434B))
        for side: CGFloat in [-1,1] {
            line([(x+side*25,y-36),(x+side*30,y-15)],white,3)
            line([(x+side*34,y+3),(x+side*40,y+25)],white,3)
        }
        box(x-94,y+43,188,12,dark,2)
        box(x-90,y+46,180,5,colour(0x718086),1)
        box(x-90,y+46,180,2,orange)
        line([(x+133,y-13),(x+133,y+41)],white,7)
        line([(x+118,y+26),(x+133,y+41),(x+148,y+26)],white,7)
    } else {
        box(x-98,y-52,196,108,dark,4)
        for offset: CGFloat in [-62,0,62] {
            box(x+offset-29,y-48,58,96,orange,3)
            disc(x+offset,y,9,dark)
            for i in 0..<8 {
                let a=CGFloat(i)*CGFloat.pi/4
                line([(x+offset+cos(a)*13,y+sin(a)*13),(x+offset+cos(a)*17,y+sin(a)*17)],dark,2)
            }
        }
        box(x-104,y+48,208,9,colour(0x718086),2)
    }
}
// One short title and one large pictogram per sign: readable at a glance from the road.
let kinds = ["Steer","Switch","Oncoming","Freeze","Ramp"]
let titles = ["STEER","SWITCH WORLDS","DODGE TRAFFIC","NIGHT FREEZES","SWITCH IN THE AIR"]
for (index,kind) in kinds.enumerated() {
    let bitmap=NSBitmapImageRep(bitmapDataPlanes:nil,pixelsWide:2400,pixelsHigh:720,bitsPerSample:8,samplesPerPixel:4,hasAlpha:true,isPlanar:false,colorSpaceName:.deviceRGB,bytesPerRow:0,bitsPerPixel:0)!
    NSGraphicsContext.saveGraphicsState();NSGraphicsContext.current=NSGraphicsContext(bitmapImageRep:bitmap)
    let transform=NSAffineTransform();transform.translateX(by:0,yBy:720);transform.scaleX(by:2,yBy:-2);transform.concat()
    NSGraphicsContext.current = NSGraphicsContext(cgContext:NSGraphicsContext.current!.cgContext,flipped:true)
    box(0,0,1200,360,white,22);box(10,10,1180,340,green,16)
    text(titles[index],600,34,52,white,true,true)
    let y: CGFloat = 226
    switch kind {
    case "Steer":
        key("A",150,y);key("←",290,y);arrow(420,y,-1)
        playerCar(600,y)
        arrow(780,y);key("→",910,y);key("D",1050,y)
    case "Oncoming":
        key("A",150,y);key("←",290,y);arrow(420,y,-1)
        oncomingVan(600,y-12)
        line([(600,278),(600,319)],white,8)
        line([(583,302),(600,319),(617,302)],white,8)
        arrow(780,y);key("→",910,y);key("D",1050,y)
    case "Switch":
        world(260,137,false);world(940,137,true)
        switchingBarrier(260,y,false);arrow(440,y);key("SPACE",600,y,200);arrow(760,y);switchingBarrier(940,y,true)
    case "Freeze":
        world(260,137,false);world(940,137,true)
        movingBlock(260,y)
        line([(200,y),(320,y)],dark,8)
        line([(218,y-17),(200,y),(218,y+17)],dark,8)
        line([(302,y-17),(320,y),(302,y+17)],dark,8)
        arrow(440,y);key("SPACE",600,y,200);arrow(760,y)
        movingBlock(940,y);box(918,y-26,14,52,dark,3);box(948,y-26,14,52,dark,3)
    default:
        world(155,140,false);world(1040,140,true)
        line([(80,294),(230,294),(330,240)],yellow,14)
        for (x,py): (CGFloat,CGFloat) in [(355,214),(397,181),(443,157),(489,143),(711,143),(757,157),(803,181),(845,214)] {disc(x,py,6,muted)}
        line([(870,294),(1120,294)],white,14)
        playerCar(200,255,0.7);playerCar(600,153,0.7);playerCar(995,255,0.7)
        key("SPACE",600,270,200)
    }
    NSGraphicsContext.restoreGraphicsState()
    try bitmap.representation(using:.png,properties:[:])!.write(to:output.appendingPathComponent(kind+".png"))
}
