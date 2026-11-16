"""Original seven-second resolving finale cue. Standard library; no samples.
Run from the repository root. Respects the game's Effects volume.
"""
from array import array
from pathlib import Path
import math, wave, uuid
rate=22050
frames=array('f',[0])*(rate*7)
# Rising open voicings resolve to a sustained D major ninth.
for start,notes,gain in [(0,[50,57,62],.10),(.65,[54,62,69],.085),(1.4,[50,57,62,66,69,76],.075)]:
    for i in range(len(frames)-round(start*rate)):
        t=i/rate
        attack=min(1,t/0.22)
        tail=max(0,1-t/(7-start))**1.5
        sample=0
        for midi in notes:
            f=440*2**((midi-69)/12)
            sample+=(math.sin(math.tau*f*t)+.16*math.sin(math.tau*f*2*t))*gain
        frames[round(start*rate)+i]+=sample*attack*tail*math.exp(-t*.24)
peak=max(abs(v) for v in frames)
path=Path('Assets/Resources/WS_Finale.wav')
with wave.open(str(path),'wb') as out:
    out.setparams((1,2,rate,0,'NONE','not compressed'))
    out.writeframes(array('h',(round(v/max(1,peak/.80)*32767) for v in frames)).tobytes())
meta=path.with_suffix('.wav.meta')
if not meta.exists():
    source=Path('Assets/Resources/WS_Victory.wav.meta').read_text()
    source=source.replace('696551d94ef4456fb566ae0a33d61b32',uuid.uuid4().hex).replace('WS_Victory','WS_Finale')
    meta.write_text(source)
print(path)
