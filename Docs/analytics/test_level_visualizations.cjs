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
