"""Original synthesized Mountain ambience. Python standard library only; no downloaded samples.
Run from the repository root. Deterministic. Writes WS_MountainWind (seamless loop), WS_EagleCry and WS_GoatBleat.
"""
from array import array
from pathlib import Path
import math, random, wave, uuid
R=22050
TAU=math.tau
OUT=Path('Assets/Resources')
rng=random.Random(4242)
def write(name, frames, loop=False):
    peak=max(abs(x) for x in frames) or 1
    if peak>.86:
        for i in range(len(frames)): frames[i]*=.86/peak
    with wave.open(str(OUT/(name+'.wav')),'wb') as w:
        w.setparams((1,2,R,0,'NONE','not compressed'))
        w.writeframes(array('h',(round(max(-1,min(1,x))*32767) for x in frames)).tobytes())
    meta=OUT/(name+'.wav.meta')
    if not meta.exists():
        template=(OUT/'WorldShiftPoliceSiren.wav.meta').read_text()
        template=template.replace('4587ba41b8d741be93823ecb33c98f9d',uuid.uuid4().hex)
        template=template.replace('Original synthesized seamless police wail, 3.2 seconds, 570-1040 Hz.','Original synthesized '+name+('. Seamless loop.' if loop else '.'))
        meta.write_text(template)
    print(name,round(len(frames)/R,2),'seconds',round(peak,3))
def render(name, duration, function, loop=False):
    n=round(duration*R)
    data=array('f',(function(i/R) for i in range(n)))
    if not loop:
        for i in range(min(300,n)):
            data[i]*=i/300;data[-1-i]*=i/300
    write(name,data,loop)

# Wind: many low partials that each complete whole cycles in the loop, under slow periodic gusts.
L=14.0
partials=[(rng.randrange(60*14,950*14)/14,rng.random()*TAU,rng.uniform(.4,1)) for _ in range(140)]
def wind(t):
    hiss=sum(a*math.sin(TAU*f*t+p)/(1+f/260) for f,p,a in partials)
    gust=.42+.30*max(0,math.sin(TAU*t/7))**2+.18*max(0,math.sin(TAU*t*3/L+1.1))**3
    return hiss*.10*gust
render('WS_MountainWind',L,wind,True)

# Eagle: a short "kee" then a long falling "keeeerrr", bright and slightly rough.
def call(t,start,length,f0,f1):
    u=t-start
    if u<0 or u>length: return 0
    x=u/length
    f=f0+(f1-f0)*x**1.4
    env=min(1,u*40)*(1-x)**1.6
    rough=1+.35*math.sin(TAU*38*u)
    return env*rough*(math.sin(TAU*f*u)+.45*math.sin(TAU*2*f*u+.6)+.2*math.sin(TAU*3*f*u))
render('WS_EagleCry',1.35,lambda t: .45*(call(t,0,.18,2800,2500)+call(t,.26,1.0,3000,1850)))

# Goat: a nasal "meh-eh-eh" with the bleat's fast vibrato and a falling pitch.
def bleat(t):
    d=.85
    x=t/d
    f=430-90*x
    vib=1+.045*math.sin(TAU*17*t)
    env=min(1,t*30)*max(0,1-x)**.8*(.75+.25*math.sin(TAU*17*t))
    s=0
    for k in range(1,14):
        fk=k*f*vib
        formant=math.exp(-((fk-850)/380)**2)+.6*math.exp(-((fk-1900)/500)**2)+.15
        s+=formant*math.sin(TAU*k*f*t*vib+k*.7)/k**.4
    return .45*env*s
render('WS_GoatBleat',.85,bleat)
