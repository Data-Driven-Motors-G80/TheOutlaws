"""Original synthesized game audio. Python standard library only; no downloaded samples.
Run from the repository root. Deterministic, cyclic loop tails wrap into the start.
Music: 110 BPM, eight bars, Am/F/C/G twice, shared rhythmic and harmonic grid.
"""
from array import array
from pathlib import Path
import math, random, struct, wave, uuid
R=22050
TAU=math.tau
OUT=Path('Assets/Resources')
rng=random.Random(731)
def write(name, channels, frames, loop=False):
    peak=max(abs(x) for x in frames) or 1
    if peak>.86:
        for i in range(len(frames)): frames[i]*=.86/peak
    with wave.open(str(OUT/(name+'.wav')),'wb') as w:
        w.setparams((channels,2,R,0,'NONE','not compressed'))
        data=array('h',(round(max(-1,min(1,x))*32767) for x in frames))
        w.writeframes(data.tobytes())
    meta=OUT/(name+'.wav.meta')
    if not meta.exists():
        template=(OUT/'WorldShiftPoliceSiren.wav.meta').read_text()
        template=template.replace('4587ba41b8d741be93823ecb33c98f9d',uuid.uuid4().hex).replace('forceToMono: 1','forceToMono: '+str(int(channels==1)))
        template=template.replace('Original synthesized seamless police wail, 3.2 seconds, 570-1040 Hz.','Original synthesized '+name+('. Seamless loop.' if loop else '.'))
        meta.write_text(template)
    print(name,len(frames)//channels/R,'seconds',round(peak,3))
def effect(name, duration, function, loop=False):
    n=round(duration*R)
    data=array('f',(function(i/R,i,n) for i in range(n)))
    if not loop:
        for i in range(min(220,n)):
            data[i]*=i/220;data[-1-i]*=i/220
    write(name,1,data,loop)
effect('WS_Engine',2,lambda t,i,n: (.28*math.sin(TAU*52*t+.8*math.sin(TAU*26*t))+.12*math.sin(TAU*104*t)+.045*math.sin(TAU*208*t))*(.85+.15*math.cos(TAU*13*t)),True)
effect('WS_Tyres',2,lambda t,i,n: (.23*math.sin(TAU*740*t+3*math.sin(TAU*19*t))+.12*math.sin(TAU*1100*t))* (.8+.2*math.sin(TAU*7*t)),True)
effect('WS_Takeoff',.48,lambda t,i,n: (rng.uniform(-1,1)*.18+math.sin(TAU*(180*t+260*t*t))*.12)*math.sin(math.pi*i/n))
effect('WS_Landing',.48,lambda t,i,n: (.6*math.sin(TAU*(85*t-50*t*t))+.24*rng.uniform(-1,1))*math.exp(-t*12))
effect('WS_Crash',.85,lambda t,i,n: (.5*rng.uniform(-1,1)+.22*math.sin(TAU*63*t)+.14*math.sin(TAU*313*t))*math.exp(-t*6))
effect('WS_Gull',1.2,lambda t,i,n: .13*math.sin(TAU*(1050*t+70*math.sin(TAU*2*t)/TAU))*(max(0,math.sin(math.pi*t/1.2))**3)*(.7+.3*math.sin(TAU*24*t)))
# Periodic layered wind/surf; each frequency completes whole cycles at the loop boundary.
phases=[rng.random()*TAU for _ in range(90)]
freqs=[rng.randrange(80,9000)/12 for _ in phases]
effect('WS_Ocean',12,lambda t,i,n: sum(math.sin(TAU*f*t+p) for f,p in zip(freqs,phases))*.025*(.63+.30*math.sin(TAU*t/6)+.07*math.cos(TAU*t/3)),True)
beat=60/110
length=round(32*beat*R)
chords=[(45,52,57,60),(41,48,53,57),(48,55,60,64),(43,50,55,59)]*2
melody=[69,72,76,72, 69,67,65,69, 67,72,76,79, 74,71,67,64]
def music(night):
    data=array('f',[0])*(length*2)
    def note(at,dur,midi,amp,kind,pan=0):
        freq=440*2**((midi-69)/12)
        count=round(dur*R);start=round(at*R)
        noise=random.Random(start+int(freq))
        for i in range(count):
            t=i/R;env=min(1,t/.006)*max(0,1-i/count)
            if kind=='pluck':
                v=(math.sin(TAU*freq*t)+.38*math.sin(TAU*2*freq*t)+.16*math.sin(TAU*3*freq*t))*math.exp(-t*7)*env
            elif kind=='bass': v=(math.sin(TAU*freq*t)+.22*math.sin(TAU*2*freq*t))*math.exp(-t*4)*env
            elif kind=='kick': v=math.sin(TAU*(48*t+9*(1-math.exp(-t*24))))*math.exp(-t*19)*env
            elif kind=='snare': v=(noise.uniform(-1,1)*.65+math.sin(TAU*180*t)*.25)*math.exp(-t*24)*env
            elif kind=='hat': v=noise.uniform(-1,1)*math.exp(-t*65)*env
            else: v=math.sin(TAU*freq*t)*math.sin(math.pi*i/count)**2
            j=((start+i)%length)*2
            data[j]+=v*amp*(1-pan*.6);data[j+1]+=v*amp*(1+pan*.6)
            if kind=='pluck':
                k=((start+i+round(beat*.75*R))%length)*2
                data[k]+=v*amp*.15*(1+pan);data[k+1]+=v*amp*.15*(1-pan)
    for bar,c in enumerate(chords):
        at=bar*4*beat
        for b in range(4):
            note(at+b*beat,.20,c[0],.22 if night else .26,'kick')
            note(at+b*beat,beat*.85,c[0]+(12 if b==3 else 0),.23,'bass')
            if b%2: note(at+b*beat,.18,60,.10 if night else .18,'snare')
            for half in range(2): note(at+(b+half*.5)*beat,.085,60,.028 if night else .052,'hat',(-1 if half else 1)*.5)
            if not night:
                for n in c[1:]: note(at+(b+.5)*beat,.45,n+12,.068,'pluck',.3)
        if night:
            for n in c[1:]: note(at,4*beat,n+12,.055,'pad',-.2)
        for b in (0,1.5,2.5,3):
            idx=(bar%4)*4+int(b)
            note(at+b*beat,.75,melody[idx]-(12 if night else 0),.10 if night else .13,'pluck',-.35)
    # Consistent gain across arrangements; don't normalize the sparse Night mix up.
    write('WS_OceanNight' if night else 'WS_OceanDay',2,data,True)
music(False);music(True)
# Short resolving motif for the completion screen.
data=array('f',[0])*(R*2)
for start,midi in [(0,69),(.12,72),(.24,76),(.42,81)]:
    for i in range(round(.9*R)):
        j=round(start*R)+i
        if j>=len(data):break
        t=i/R;f=440*2**((midi-69)/12)
        data[j]+=.23*math.sin(TAU*f*t)*min(1,t*120)*math.exp(-t*5)
write('WS_Victory',1,data)
