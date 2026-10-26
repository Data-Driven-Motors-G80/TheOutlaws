"""Generate the original, geometric HUD icon set. Run with Python + Pillow.
No downloaded art or fonts; all shapes below are authored for this project.
"""
from pathlib import Path
from PIL import Image, ImageDraw
import math

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/Resources/BackpackIcons'
S = 4
SIZE = 128

def make(kind):
    image = Image.new('RGBA', (SIZE*S, SIZE*S))
    d = ImageDraw.Draw(image)
    def box(bounds, fill, radius=0):
        d.rounded_rectangle(tuple(v*S for v in bounds), radius=radius*S, fill=fill)
    def poly(points, fill): d.polygon([(x*S,y*S) for x,y in points],fill=fill)
    def circle(bounds,fill): d.ellipse(tuple(v*S for v in bounds),fill=fill)
    def stroke(points, fill, width):
        d.line([(x*S,y*S) for x,y in points],fill=fill,width=width*S,joint='curve')
        r=width/2
        for x,y in points: circle((x-r,y-r,x+r,y+r),fill)
    white='#F1FAFF'
    if kind == 'shield':
        poly([(64,12),(107,28),(102,70),(89,91),(64,114),(39,91),(26,70),(21,28)],'#1473AE')
        poly([(64,19),(99,33),(94,68),(82,87),(64,103),(46,87),(34,68),(29,33)],'#55D5F0')
        poly([(64,19),(99,33),(94,68),(82,87),(64,103)],'#28A8DB')
        stroke([(44,60),(58,74),(85,45)],white,9)
    elif kind == 'nitro':
        points=[(70,9),(27,69),(57,69),(46,116),(103,48),(73,48),(85,9)]
        poly([(x,y+4) for x,y in points],'#C56716')
        poly(points,'#FFB82E')
        poly([(70,9),(36,61),(66,61),(56,97),(94,52),(65,52),(78,9)],'#FFE578')
    elif kind == 'gun':
        poly([(39,58),(67,58),(62,109),(34,109)],'#52748D')
        box((15,32,109,62),'#329CC4',6)
        box((21,23,106,49),'#A5E3F6',4)
        box((105,31,117,47),'#52748D',2)
        box((23,18,32,26),'#D5F4FD',2)
        box((94,18,103,26),'#D5F4FD',2)
        stroke([(66,63),(85,63),(85,81),(66,81)],'#A5E3F6',6)
        box((41,77,56,84),'#2F506C',2)
        box((40,91,55,98),'#2F506C',2)
    elif kind == 'jammer':
        for radius in (28,43):
            for start,end in ((205,270),(-90,-25)):
                d.arc(((64-radius)*S,(46-radius)*S,(64+radius)*S,(46+radius)*S),start,end,fill='#BC9DFF',width=7*S)
        stroke([(64,31),(64,62)],'#E6DCFF',6)
        circle((59,26,69,36),'#F1EAFF')
        box((39,58,89,114),'#7046C3',9)
        box((45,64,83,108),'#A37BE9',6)
        box((52,73,76,87),'#E7DEFF',3)
        circle((54,94,61,101),'#4A2C85')
        circle((68,94,75,101),'#4A2C85')
    elif kind == 'reverse':
        stroke([(24,44),(101,44)],'#BD9AFF',10)
        poly([(101,44),(80,24),(80,64)],'#BD9AFF')
        stroke([(104,87),(27,87)],'#E2D3FF',10)
        poly([(27,87),(48,67),(48,107)],'#E2D3FF')
    elif kind == 'sun':
        for i in range(8):
            a=i*math.pi/4
            stroke([(64+math.cos(a)*41,64+math.sin(a)*41),(64+math.cos(a)*51,64+math.sin(a)*51)],'#FFC344',7)
        circle((33,33,95,95),'#EBA122')
        circle((36,32,92,88),'#FFDB5C')
        circle((46,42,60,56),'#FFF0A7')
    else:
        stroke([(47,34),(47,23),(81,23),(81,34)],'#8095AA',7)
        box((29,32,99,113),'#62778B',13)
        box((35,38,93,107),'#8295A6',10)
        box((40,72,88,99),'#53697D',6)
        stroke([(47,80),(81,80)],'#B8C7D3',4)
        box((60,48,68,63),'#CEDAE3',3)
    return image.resize((SIZE,SIZE),Image.Resampling.LANCZOS)

if __name__ == '__main__':
    OUT.mkdir(parents=True,exist_ok=True)
    for kind in ('empty','gun','shield','nitro','jammer','sun','reverse'):
        make(kind).save(OUT / (kind+'.png'))
