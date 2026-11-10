const fs = require('node:fs'), vm = require('node:vm'), assert = require('node:assert/strict');
const ctx = vm.createContext({console});
for (const f of ['WeeWooAnalytics.gs','Visualizations.gs']) vm.runInContext(fs.readFileSync(__dirname+'/'+f,'utf8'),ctx);
const base = {ts:'2026-11-08T12:00:00Z',build:'beta-levels',level:0,sections:6,section_at_end:0,outcome:'menu',switches:10,switch_deaths:1};
const row = (id, extra) => ({...base,attempt_id:id,player_id:id,...extra});
const data = [row('done',{outcome:'complete',s_0:10,s_5:30,deaths_5:2, ramp_attempts_1:3,ramp_success_1:1,ramp_failed_1:1,ramp_aborted_1:1,ramp_first_1:0}),
 row('partial',{s_0:2}), row('testing',{unlock_all:true,outcome:'complete',s_0:1000}),
 row('editor',{build:'beta-editor',outcome:'complete',s_0:1000}),
 row('desert',{level:2,sections:7,s_6:12,deaths_6:3}),
 row('done',{outcome:'tab_closed',s_0:999}), row('old',{ts:'2020-01-01',s_0:999})];
const result = ctx.wlVizCompute({ws_level_attempts:data},{from:Date.parse('2026-01-01'),until:null,builds:ctx.VIS_BUILDS[1]},'UTC');
assert.equal(result.kpis[0][1],'3');
assert.equal(result.sections[0].charts[0].rows[0][1],10); // unfinished lesson excluded
assert.equal(result.sections[0].charts[0].rows[1][1],30); // sixth lesson retained
assert.equal(result.sections[0].charts[1].rows[0][1],1);
assert.equal(result.sections[1].charts.flatMap(c=>c.rows).find(r=>r[0].startsWith('Desert C7'))[1],3);
assert.equal(result.sections[2].charts[0].rows[0][1],10);
assert.equal(result.sections[3].charts[0].rows[0][1],50); // aborted excluded
assert.equal(result.kpis[2][1],'0% (n=1)');
const empty=ctx.wlVizCompute({ws_level_attempts:[row('no-switch',{switches:0})]},{builds:ctx.VIS_BUILDS[1]},'UTC');
assert.equal(empty.sections[2].charts[0].rows.length,0);
assert.equal(empty.sections[3].charts[0].rows.length,0); // legacy rows aren't failed ramps
console.log('PASS: filtering, duplicate uploads, completed lessons, seven checkpoint intervals, zero denominators, resolved ramp rate, first tries.');

// Sign reading (section 5) and crash locations (section 6).
const signs = [
  row('reader',{outcome:'complete',section_at_end:5,s_0:8,read_0:6.5,tries_0:1,first_try_0:1,read_1:4,tries_1:2,first_try_1:0,fails_1:'210:M:switched_into',read_s:30,drive_s:90,real_s:140,freeze_demo_s:4}),
  row('skipper',{outcome:'complete',section_at_end:5,s_0:9,read_0:0.6,tries_0:3,first_try_0:0,fails_0:'88:M:crash;89:L:crash',read_s:5,drive_s:120,real_s:130,freeze_demo_s:-1}),
  row('quitter',{s_0:3,read_0:0.4,tries_0:2,first_try_0:0,fails_0:'88:M:crash'}),
  row('city',{level:1,sections:5,s_0:20,tries_0:2,first_try_0:0,fails_0:'150:R:switched_into'})];
const s5 = ctx.wlVizCompute({ws_level_attempts:signs},{builds:ctx.VIS_BUILDS[1]},'UTC');
const sec5 = s5.sections[4], sec6 = s5.sections[5];
assert.equal(sec5.charts[0].rows[0][1],0.6); // median first read of Steer: 0.4, 0.6, 6.5
assert.equal(sec5.charts[1].rows[0][1],66.7); // 2 of 3 Steer views under 2 s
assert.equal(JSON.stringify(sec5.charts[2].rows[0].slice(1)),JSON.stringify([100,0])); // reader cleared first try, skippers did not
assert.equal(sec5.charts[3].rows.length,4); // one point per resolved lesson attempt with a reading time
assert.equal(sec5.charts[4].rows[0][1],2); // mean tries among cleared Steer: (1+3)/2
assert.equal(JSON.stringify(sec5.charts[5].rows[0]),JSON.stringify(['Steer',3,0,0,0,0]));
assert.equal(JSON.stringify(sec5.charts[6].rows[0]),JSON.stringify(['Reading signs',17.5]));
assert.equal(s5.kpis[3][1],'66.7% (2/3)');
assert.equal(JSON.stringify(sec6.charts[0].rows[0]),JSON.stringify(['Training C1',3,0,0,0,0]));
assert.ok(sec6.charts.some(c=>c.title.startsWith('City')));
const hot = sec6.charts[sec6.charts.length-1].rows[0];
assert.equal(JSON.stringify(hot),JSON.stringify(['Training C1 · 80 m · M lane · Hit obstacle',2]));
const legacy = ctx.wlVizCompute({ws_level_attempts:[row('perfect',{outcome:'complete',section_at_end:5,read_0:3,read_5:5,tries_5:1,first_try_5:-1,s_5:10})]},{builds:ctx.VIS_BUILDS[1]},'UTC');
assert.equal(JSON.stringify(legacy.sections[4].charts[2].rows[0].slice(1)),JSON.stringify([100,0])); // last lesson of a finished run counts as cleared
console.log('PASS: sign reading time, skip share, read-vs-skip first tries, tries, failure causes, time split, crash hotspots.');
