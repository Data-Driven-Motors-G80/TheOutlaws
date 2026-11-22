// Normalize/fade the original speech and mix a seamless, diffuse stereo crowd bed.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const out = 'Assets/Resources';
const folder = path.join(out, 'WorldShiftCityVoices');
const rate = 22050;
function read(file) {
  const b = fs.readFileSync(file);
  let start, length;
  for (let p = 12; p + 8 <= b.length;) {
    const size = b.readUInt32LE(p + 4), tag = b.toString('ascii', p, p + 4);
    if (tag === 'fmt ' && (b.readUInt16LE(p+8) !== 1 || b.readUInt16LE(p+10) !== 1 || b.readUInt32LE(p+12) !== rate || b.readUInt16LE(p+22) !== 16)) throw Error('Expected mono 16-bit 22050Hz PCM');
    if (tag === 'data') { start = p + 8; length = size; break; }
    p += 8 + size + (size % 2);
  }
  if (!start) throw Error('Missing PCM data');
  return Float32Array.from({length: length/2}, (_, i) => b.readInt16LE(start+i*2)/32768);
}
function write(file, samples, channels) {
  const b = Buffer.alloc(44 + samples.length*2);
  b.write('RIFF'); b.writeUInt32LE(b.length-8,4); b.write('WAVEfmt ',8);
  b.writeUInt32LE(16,16); b.writeUInt16LE(1,20); b.writeUInt16LE(channels,22);
  b.writeUInt32LE(rate,24); b.writeUInt32LE(rate*channels*2,28); b.writeUInt16LE(channels*2,32); b.writeUInt16LE(16,34);
  b.write('data',36); b.writeUInt32LE(samples.length*2,40);
  for (let i=0; i<samples.length; i++) b.writeInt16LE(Math.round(Math.max(-1,Math.min(1,samples[i]))*32767),44+i*2);
  fs.writeFileSync(file,b);
  if (!fs.existsSync(file+'.meta')) {
    let meta = fs.readFileSync(path.join(out,'WS_Gull.wav.meta'),'utf8');
    meta = meta.replace(/^guid: .*$/m, 'guid: '+crypto.randomUUID().replaceAll('-',''))
      .replace('forceToMono: 1','forceToMono: '+(channels===1 ? '1' : '0'))
      .replace(/^  userData: .*$/m,'  userData: Original City dialogue synthesized with Windows David and Zira.');
    fs.writeFileSync(file+'.meta',meta);
  }
}
const clips = fs.readdirSync(folder).filter(f=>f.endsWith('.wav')).sort().map(file=> {
  let s=read(path.join(folder,file));
  let first=s.findIndex(v=>Math.abs(v)>.006), last=s.length-1;
  while(last>first && Math.abs(s[last])<.006) last--;
  if (first<0) throw Error('Silent speech');
  s=s.slice(Math.max(0,first-441),Math.min(s.length,last+882));
  let peak=0; for(const v of s) peak=Math.max(peak,Math.abs(v));
  for(let i=0;i<s.length;i++) s[i]*=.72/peak*Math.min(1,i/220,(s.length-1-i)/330);
  write(path.join(folder,file),s,1); return s;
});
const frames=rate*24, bed=new Float32Array(frames*2);
let seed=731;
const random=()=>{ seed=(1664525*seed+1013904223)>>>0; return seed/4294967296; };
for(let voice=0;voice<100;voice++) {
  const clip=clips[Math.floor(random()*clips.length)], start=Math.floor(random()*frames);
  const speed=.87+random()*.28, pan=.1+random()*.8, gain=.28+random()*.32;
  let low=0;
  for(let i=0;i<(clip.length-1)/speed;i++) {
    const x=i*speed, k=Math.floor(x), v=clip[k]*(1-x+k)+clip[k+1]*(x-k);
    low+=.28*(v-low);
    const f=(start+i)%frames;
    bed[f*2]+=low*gain*Math.sqrt(1-pan); bed[f*2+1]+=low*gain*Math.sqrt(pan);
  }
}
let peak=0; for(const v of bed) peak=Math.max(peak,Math.abs(v));
for(let i=0;i<bed.length;i++) bed[i]*=.68/peak;
write(path.join(out,'WS_CityCrowd.wav'),bed,2);
console.log(`Generated ${clips.length} voiced fragments and a 24-second stereo crowd loop.`);
