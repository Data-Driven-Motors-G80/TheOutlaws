#!/usr/bin/env python3
"""Generate only the original, seamless sleigh-bell/air loop used by Snow's Santa flyover."""
from array import array
from pathlib import Path
import math
import random
import uuid
import wave

RATE = 22050
DURATION = 4.0
OUT = Path(__file__).resolve().parents[2] / 'Assets/Resources/WS_SantaBells.wav'
rng = random.Random(2491)
strikes = [(i * .25 + rng.uniform(-.025, .025), rng.choice([1640, 1810, 1990, 2230]), rng.uniform(.6, 1)) for i in range(16)]
air = [(rng.randrange(120, 2000) / DURATION, rng.random() * math.tau) for _ in range(18)]
samples = []
for i in range(int(RATE * DURATION)):
    t = i / RATE
    value = 0.0
    for start, frequency, gain in strikes:
        age = (t - start) % DURATION
        attack = min(1, age / .002)
        for ratio, amplitude, decay in [(1, .14, 7), (1.37, .09, 9), (1.91, .055, 12), (2.62, .025, 16)]:
            value += gain * amplitude * attack * math.exp(-age * decay) * math.sin(math.tau * frequency * ratio * age)
    value += sum(math.sin(math.tau * f * t + phase) for f, phase in air) * .0018
    samples.append(value)
peak = max(abs(v) for v in samples)
scale = .72 / peak
pcm = array('h', (round(v * scale * 32767) for v in samples))
with wave.open(str(OUT), 'wb') as audio:
    audio.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
    audio.writeframes(pcm.tobytes())
meta = OUT.with_suffix('.wav.meta')
if not meta.exists():
    template = (OUT.parent / 'WS_Gull.wav.meta').read_text()
    lines = template.splitlines()
    for i, line in enumerate(lines):
        if line.startswith('guid: '):
            lines[i] = 'guid: ' + uuid.uuid4().hex
        if 'userData:' in line:
            lines[i] = '  userData: Original synthesized seamless sleigh bells and soft flyby air, 4 seconds. Tools/Audio/generate_santa_audio.py.'
    meta.write_text('\n'.join(lines) + '\n')
print(f'{OUT.name}: {DURATION}s, mono, {RATE} Hz, peak .72')
