/**
 * Wee Woo Run analytics receiver + dashboard (Google Apps Script).
 *
 * Setup (once, ~3 minutes) - see Docs/ANALYTICS.md:
 *  1. Open the responses Google Sheet > Extensions > Apps Script.
 *  2. Replace the editor contents with this file and save.
 *  3. Deploy > New deployment > type "Web app"; Execute as: Me; Who has access: Anyone.
 *  4. Copy the web app URL (ends in /exec) into Assets/Resources/WeeWooAnalytics.json "endpointUrl".
 *
 * The game POSTs {"rows":[{"sheet":"events","data":{...}}, ...]} as text/plain.
 * Each sheet gets new columns automatically the first time a field appears.
 */

var SHEETS = ['events', 'runs', 'sessions', 'tutorials'];
var DASHBOARD = 'dashboard';
// Current tutorial lessons in play order: [step name sent by the game, column prefix, label].
// Older builds' lessons (Shield before the mystery boxes, Reverse, Recovery, PursuitCollision) stay in the raw tabs.
var TUTORIAL_LESSONS = [
  ['Intro', 'intro', 'Escape the cops'], ['Steer', 'steer', 'Steer'], ['Dodge', 'dodge', 'Avoid obstacles'],
  ['Fuel', 'fuel', 'Fuel low'], ['Ammo', 'ammo', 'Grab 3 ammo'], ['Shoot', 'shoot', 'Destroy the 1 wall'],
  ['ShootTwice', 'shoottwice', 'Destroy the 2 wall'], ['Shield', 'shield', 'Shield box + wall'],
  ['MysteryDizzy', 'mysterydizzy', 'Dizzy box'], ['Nitro', 'nitro', 'Nitro box (reversed)']
];

/** "Step:what;Step:what" (fail_log) or "Step:what=result;..." (pass_log) from tutorial rows, counted. */
function tutorialLogCounts(rows, key) {
  var c = {};
  rows.forEach(function (r) {
    String(r[key] || '').split(';').forEach(function (item) {
      if (item) c[item] = (c[item] || 0) + 1;
    });
  });
  return Object.keys(c).sort(function (a, b) { return c[b] - c[a]; }).map(function (k) { return [k, c[k]]; });
}

/** Rows that report a lesson (the current tutorial records <prefix>_attempts). */
function tutorialLessonRows(rows, prefix) {
  return rows.filter(function (r) { return r[prefix + '_s'] !== '' && r[prefix + '_s'] !== undefined && r[prefix + '_s'] !== null; });
}

/** % of a lesson's passes on the first try, from rows that record tries (older builds did not). */
function tutorialFirstTryPct(rows, prefix) {
  var tracked = rows.filter(function (r) { return r[prefix + '_attempts'] !== '' && r[prefix + '_attempts'] !== undefined && r[prefix + '_attempts'] !== null; });
  return tracked.filter(function (r) { return truthy(r[prefix + '_first_try']); }).length / (tracked.length || 1) * 100 * (tracked.length ? 1 : NaN);
}

function doPost(e) {
  var body;
  try {
    body = JSON.parse(e.postData.contents);
  } catch (err) {
    return reply({ ok: false, error: 'bad json' });
  }
  var rows = (body && body.rows) || [];
  if (!rows.length) return reply({ ok: true, written: 0 });

  // Group by sheet so each sheet is written with a single setValues call.
  var bySheet = {};
  rows.forEach(function (row) {
    if (!row || SHEETS.indexOf(row.sheet) < 0 || !row.data || typeof row.data !== 'object' || Array.isArray(row.data)) return;
    (bySheet[row.sheet] = bySheet[row.sheet] || []).push(row.data);
  });

  var lock = LockService.getScriptLock();
  lock.waitLock(25000);
  try {
    var book = SpreadsheetApp.getActiveSpreadsheet();
    var written = 0;
    Object.keys(bySheet).forEach(function (name) {
      written += appendRows(book, name, bySheet[name]);
    });
    // Only summary uploads refresh reports; frequent event batches remain inexpensive.
    // A report failure must not reject already-saved rows and provoke duplicate uploads.
    var reportError = '';
    if (bySheet.runs || bySheet.tutorials) {
      try { updateMetricTabs(book); }
      catch (err) { reportError = String(err); console.error(reportError); }
    }
    return reply({ ok: true, written: written, metricTabsError: reportError });
  } finally {
    lock.releaseLock();
  }
}

function doGet() {
  return reply({ ok: true, service: 'wee-woo-run analytics' });
}

function reply(obj) {
  return ContentService.createTextOutput(JSON.stringify(obj)).setMimeType(ContentService.MimeType.JSON);
}

function appendRows(book, name, records) {
  var sheet = book.getSheetByName(name) || book.insertSheet(name);
  var lastColumn = sheet.getLastColumn();
  var headers = lastColumn > 0 ? sheet.getRange(1, 1, 1, lastColumn).getValues()[0] : [];
  var added = false;
  records.forEach(function (record) {
    Object.keys(record).forEach(function (key) {
      if (headers.indexOf(key) < 0) {
        headers.push(key);
        added = true;
      }
    });
  });
  if (added) {
    sheet.getRange(1, 1, 1, headers.length).setValues([headers]).setFontWeight('bold');
    sheet.setFrozenRows(1);
  }
  var values = records.map(function (record) {
    return headers.map(function (key) {
      var v = record[key];
      return v === undefined || v === null ? '' : v;
    });
  });
  sheet.getRange(sheet.getLastRow() + 1, 1, values.length, headers.length).setValues(values);
  return values.length;
}

// ---------------------------------------------------------------- dashboard

function onOpen() {
  SpreadsheetApp.getUi()
    .createMenu('Wee Woo Analytics')
    .addItem('Create / refresh metric tabs', 'refreshMetricTabs')
    .addItem('Refresh dashboard', 'refreshDashboard')
    .addToUi();
}

/** Reads every sheet and writes the key findings to the "dashboard" tab. */
function refreshDashboard() {
  var book = SpreadsheetApp.getActiveSpreadsheet();
  // Editor play ("-editor") and setup/test rows stay in the tabs but are left out of the numbers.
  // Retried uploads can occasionally land twice, so each tab is de-duplicated by its natural key.
  var runs = dedupe(real(table(book, 'runs')), function (r) { return r.run_id; });
  var events = dedupe(real(table(book, 'events')), function (r) { return r.session_id + '#' + r.seq; });
  var tutorials = dedupe(real(table(book, 'tutorials')), function (r) { return r.session_id + '#' + r.status + '#' + r.seconds; });
  var sessions = dedupe(real(table(book, 'sessions')), function (r) { return r.session_id; });
  var out = [];

  function section(title) { out.push(['']); out.push([title]); }
  function row(label, value, note) { out.push([label, value === undefined ? '' : value, note || '']); }

  var players = unique(events, 'player_id');
  var sessionIds = unique(events, 'session_id');
  var completedRuns = runs.filter(function (r) { return r.end_reason === 'police' || r.end_reason === 'fuel'; });

  section('AUDIENCE');
  row('Unique players (browsers)', players.length);
  row('Sessions (page loads)', sessionIds.length);
  row('Runs recorded', runs.length, 'includes quits and restarts');
  row('Runs per player', ratio(runs.length, players.length));
  row('Players who never pressed START', players.length - unique(filter(events, 'ev', 'start_pressed'), 'player_id').length,
      'bounced on the title screen');
  row('Median seconds on title before START', median(num(filter(events, 'ev', 'start_pressed'), 'title_s')));
  row('Players who toggled the tutorial switch', unique(filter(events, 'ev', 'tutorial_toggled'), 'player_id').length);

  section('TUTORIAL FUNNEL (% of tutorial starters reaching each step)');
  var starters = unique(filter(events, 'ev', 'tutorial_start'), 'session_id').length;
  row('Tutorials started', starters);
  TUTORIAL_LESSONS.forEach(function (lesson) {
    var step = lesson[0];
    var done = filter(filter(events, 'ev', 'tutorial_step_complete'), 'step', step);
    var firstDone = dedupe(done, function (r) { return r.session_id; });
    var retries = filter(filter(events, 'ev', 'tutorial_retry'), 'step', step).length;
    var firstTry = firstDone.filter(function (r) { return truthy(r.first_try); }).length;
    row('  completed ' + lesson[2], pct(unique(done, 'session_id').length, starters),
        'median ' + median(num(firstDone, 'seconds')) + ' s, passed first try ' + pct(firstTry, firstDone.length) +
        ', median tries ' + median(num(firstDone, 'attempts')) + ', ' + retries + ' retries total, dry fires ' + sum(num(firstDone, 'dry_fires')));
  });
  var completedTut = filter(tutorials, 'status', 'completed');
  row('Tutorial completion rate', pct(completedTut.length, starters));
  row('Median tutorial length (s)', median(num(completedTut, 'seconds')));
  var abandoned = tutorials.filter(function (t) { return t.status === 'abandoned' || t.status === 'restarted'; });
  row('Tutorials abandoned / restarted', abandoned.length, 'quit step counts: ' + counts(abandoned, 'quit_step'));
  row('Most common retry reasons', counts(filter(events, 'ev', 'tutorial_retry'), 'reason'));
  row('Where players failed (lesson:object)', tutorialLogCounts(tutorials, 'fail_log').slice(0, 12).map(function (p) { return p[0] + ' ' + p[1]; }).join(', '));
  row('How players got past objects', tutorialLogCounts(tutorials, 'pass_log').slice(0, 12).map(function (p) { return p[0] + ' ' + p[1]; }).join(', '));

  section('RUNS');
  var distances = num(completedRuns, 'distance_m');
  row('Completed runs (caught or out of fuel)', completedRuns.length);
  row('Median distance (m)', median(distances));
  row('Mean distance (m)', mean(distances));
  row('Best distance (m)', distances.length ? Math.max.apply(null, distances) : '');
  [500, 1000, 1500, 2000, 2500].forEach(function (m) {
    row('  % runs reaching ' + m + ' m', pct(distances.filter(function (d) { return d >= m; }).length, distances.length));
  });
  row('% runs reaching snow', pct(completedRuns.filter(function (r) { return truthy(r.reached_snow); }).length, completedRuns.length));
  row('End reasons', counts(runs, 'end_reason'), 'quit_tab_closed = closed the tab mid-run');
  row('Caught within 5 s of a hit', pct(completedRuns.filter(function (r) { return truthy(r.caught_within_5s_of_hit); }).length,
      filter(completedRuns, 'end_reason', 'police').length), 'of police deaths: hits are what kill');
  row('Median distance: first run vs 3rd+ run', median(num(filter(completedRuns, 'run_number_session', 1), 'distance_m')) + ' vs ' +
      median(num(completedRuns.filter(function (r) { return Number(r.run_number_session) >= 3; }), 'distance_m')), 'learning curve');
  row('Median distance: tutorial ON vs OFF', median(num(completedRuns.filter(function (r) { return truthy(r.tutorial_was_on); }), 'distance_m')) +
      ' vs ' + median(num(completedRuns.filter(function (r) { return !truthy(r.tutorial_was_on); }), 'distance_m')));

  section('HOW LONG PLAYERS LAST');
  var secs = num(completedRuns, 'survival_s');
  row('Median run length (s)', median(secs), 'active driving time, pauses excluded');
  row('Mean run length (s)', mean(secs));
  row('Longest run (s)', secs.length ? Math.max.apply(null, secs) : '');
  [30, 60, 90, 120, 180].forEach(function (t) {
    row('  % runs lasting ' + t + ' s+', pct(secs.filter(function (v) { return v >= t; }).length, secs.length));
  });

  section('TOP 10 RUNS (furthest) - the most informative players');
  out.push(['distance / time / player', 'death cause / hits / shots', 'destroyed / dodged / avg ammo / missions']);
  completedRuns.slice().sort(function (a, b) { return Number(b.distance_m) - Number(a.distance_m); }).slice(0, 10).forEach(function (r) {
    out.push([r.distance_m + ' m / ' + r.survival_s + ' s / ' + r.player_id + ' (run ' + r.run_number_total + ')',
      r.death_cause + ' / ' + r.hits_total + ' hits / ' + r.shots_fired + ' shots (' + r.accuracy_pct + '%)',
      (Number(r.destroyed_green) + Number(r.destroyed_yellow) + Number(r.destroyed_red)) + ' / ' +
      (Number(r.dodged_green) + Number(r.dodged_yellow) + Number(r.dodged_red)) + ' / ' + r.avg_ammo_held + ' / ' + (r.missions_completed || '-')]);
  });

  section('CAUSE OF DEATH');
  row('Death causes', counts(completedRuns, 'death_cause'),
      'police_right_after_hit = caught within 5 s of a hit; fuel_missed_tanks = drove past at least one F');
  row('Hits in the last 10 s before ending (avg)', mean(num(completedRuns, 'hits_last_10s')));
  row('Died while still holding bullets', pct(completedRuns.filter(function (r) { return Number(r.ammo_at_end) > 0; }).length, completedRuns.length),
      'high = players hoard ammo instead of using it');
  row('Colour of the last block hit', counts(completedRuns.filter(function (r) { return r.last_hit_colour; }), 'last_hit_colour'));

  section('HOW PLAYERS GET PAST BLOCKS (dodged / hit / shot down)');
  ['green', 'yellow', 'red'].forEach(function (c) {
    var d = sum(num(completedRuns, 'dodged_' + c)), h = sum(num(completedRuns, 'hits_' + c)), x = sum(num(completedRuns, 'destroyed_' + c));
    var total = d + h + x;
    row('  ' + c + ' (' + (c === 'green' ? '1' : c === 'yellow' ? '2' : '3') + ' shots)',
        pct(d, total) + ' dodged / ' + pct(h, total) + ' hit / ' + pct(x, total) + ' destroyed', total + ' blocks');
  });
  row('Shots aimed at green / yellow / red', sum(num(completedRuns, 'shots_on_green')) + ' / ' +
      sum(num(completedRuns, 'shots_on_yellow')) + ' / ' + sum(num(completedRuns, 'shots_on_red')));

  section('BULLET USAGE');
  row('Shots per run (mean)', mean(num(completedRuns, 'shots_fired')));
  row('Average bullets carried', mean(num(completedRuns, 'avg_ammo_held')), 'out of 5');
  row('Median seconds a bullet is held before firing', median(num(completedRuns, 'avg_hold_s')),
      'low = used right away; high = hoarded');
  row('% of run time with full ammo (5/5)', pct(sum(num(completedRuns, 's_ammo_full')), sum(num(completedRuns, 'survival_s'))));
  row('% of run time with no ammo', pct(sum(num(completedRuns, 's_ammo_empty')), sum(num(completedRuns, 'survival_s'))));
  var cps = filter(events, 'ev', 'checkpoint');
  [250, 500, 1000, 1500, 2000, 2500, 3000].forEach(function (m) {
    var at = filter(cps, 'm', m);
    if (at.length) row('  shots per 250 m around ' + m + ' m', mean(num(at, 'shots_since')),
        'ammo held ' + mean(num(at, 'ammo')) + ', destroyed ' + mean(num(at, 'destroyed_since')) + ', dodged ' + mean(num(at, 'dodged_since')) + ', hits ' + mean(num(at, 'hits_since')));
  });

  section('WHAT PLAYERS GO FOR (collected / appeared)');
  row('F fuel', pct(sum(num(completedRuns, 'fuel_collected')), sum(num(completedRuns, 'fuel_spawned'))));
  row('A ammo', pct(sum(num(completedRuns, 'ammo_collected')), sum(num(completedRuns, 'ammo_spawned'))));
  row('? mystery', pct(sum(num(completedRuns, 'mystery_collected')), sum(num(completedRuns, 'mystery_spawned'))));

  section('POWER-UPS');
  row('From ? boxes: shield / nitro / jammer / reversed', sum(num(completedRuns, 'mystery_shield')) + ' / ' +
      sum(num(completedRuns, 'mystery_nitro')) + ' / ' + sum(num(completedRuns, 'mystery_recovery')) + ' / ' + sum(num(completedRuns, 'mystery_reverse')));
  row('Hits blocked by a shield', sum(num(completedRuns, 'hits_shielded')));
  row('Nitro lost to a hit', sum(num(completedRuns, 'hits_with_nitro')));
  row('Hits while controls were swapped', sum(num(completedRuns, 'hits_while_reversed')),
      'per swapped second: ' + ratio(sum(num(completedRuns, 'hits_while_reversed')), sum(num(completedRuns, 's_reversed'))));

  section('UNDERSTANDING CHECKS (possible misunderstandings)');
  row('% runs with zero shots fired', pct(completedRuns.filter(function (r) { return Number(r.shots_fired) === 0; }).length, completedRuns.length),
      'high = players ignore shooting');
  row('Shot accuracy (hits / shots)', pct(sum(num(completedRuns, 'shots_hit')), sum(num(completedRuns, 'shots_fired'))));
  row('Shots that hit nothing (range or aim)', sum(num(completedRuns, 'shots_missed')));
  row('Dry fires per run (SPACE with 0 ammo)', ratio(sum(num(completedRuns, 'dry_fires')), completedRuns.length), 'high = ammo squares unclear');
  row('Hits where player had enough ammo to clear it', pct(filter(filter(events, 'ev', 'collision'), 'could_shoot', true).length,
      filter(events, 'ev', 'collision').length));
  row('Fuel pickup rate (collected / spawned)', pct(sum(num(completedRuns, 'fuel_collected')), sum(num(completedRuns, 'fuel_spawned'))));
  row('Ammo pickup rate', pct(sum(num(completedRuns, 'ammo_collected')), sum(num(completedRuns, 'ammo_spawned'))));
  row('Ammo grabbed while already full', sum(num(completedRuns, 'ammo_wasted_full')));
  row('Mystery ? pickup rate', pct(sum(num(completedRuns, 'mystery_collected')), sum(num(completedRuns, 'mystery_spawned'))),
      'low = players avoid ? boxes');
  row('Hits while controls swapped (per swap-second)', ratio(sum(num(completedRuns, 'hits_while_reversed')), sum(num(completedRuns, 's_reversed'))));
  row('Fuel deaths that had a tank missed', pct(filter(completedRuns, 'end_reason', 'fuel').filter(function (r) { return Number(r.fuel_missed) > 0; }).length,
      filter(completedRuns, 'end_reason', 'fuel').length));
  row('Collisions by colour (green / yellow / red)', sum(num(completedRuns, 'hits_green')) + ' / ' + sum(num(completedRuns, 'hits_yellow')) + ' / ' + sum(num(completedRuns, 'hits_red')));
  row('Median % of time steering', median(num(completedRuns, 'steer_pct')));

  section('MISSIONS (players who completed each)');
  row('Missions completed per run (mean)', mean(num(completedRuns, 'missions_completed_count')));
  row('Players with 3+ / 6+ / 10 missions', [3, 6, 10].map(function (n) {
    var best = {};
    runs.forEach(function (r) { best[r.player_id] = Math.max(best[r.player_id] || 0, Number(r.missions_done_total) || 0); });
    return Object.keys(best).filter(function (p) { return best[p] >= n; }).length;
  }).join(' / '));
  var missionEvents = filter(events, 'ev', 'mission_complete');
  for (var id = 1; id <= 10; id++) {
    var hits = filter(missionEvents, 'mission_id', id);
    row('  #' + id + ' ' + (hits.length ? hits[0].mission : ''), unique(hits, 'player_id').length,
        hits.length ? 'median run #' + median(num(hits, 'run_number_total')) + ' when completed' : '');
  }

  section('ENGAGEMENT');
  row('Pauses per run', ratio(sum(num(runs, 'pauses')), runs.length));
  row('Focus losses (clicked/tabbed away)', sum(num(runs, 'focus_losses')));
  row('Restarts from pause menu', filter(runs, 'end_reason', 'restart_from_pause').length);
  row('Median session length (s)', median(num(sessions, 'session_s')));
  row('Where sessions ended', counts(sessions, 'quit_at'));

  var sheet = book.getSheetByName(DASHBOARD) || book.insertSheet(DASHBOARD, 0);
  sheet.clear();
  out.unshift(['Wee Woo Run analytics', 'refreshed ' + new Date().toLocaleString(), '']);
  var width = 3;
  var padded = out.map(function (r) { while (r.length < width) r.push(''); return r; });
  sheet.getRange(1, 1, padded.length, width).setValues(padded);
  sheet.getRange(1, 1).setFontWeight('bold').setFontSize(14);
  padded.forEach(function (r, i) { if (r[0] && r[0] === r[0].toUpperCase() && !r[1]) sheet.getRange(i + 1, 1).setFontWeight('bold'); });
  sheet.setColumnWidth(1, 380);
  sheet.setColumnWidth(2, 220);
  sheet.setColumnWidth(3, 420);
}

// ---------------------------------------------------------------- automatic metric tabs

var METRIC_TAB_MARKER = 'WeeWoo managed metric report v1';

/** Run once from the editor to backfill existing records; doPost then keeps these current. */
function refreshMetricTabs() {
  var lock = LockService.getScriptLock();
  lock.waitLock(25000);
  try { updateMetricTabs(SpreadsheetApp.getActiveSpreadsheet()); }
  finally { lock.releaseLock(); }
}

function metricRuns(rows) {
  var byId = {}, anonymous = [];
  metricReal(rows).forEach(function (r) {
    if (!r.run_id) { anonymous.push(r); return; }
    var previous = byId[r.run_id];
    // A page-close beacon can overlap a normal run-end upload.
    if (!previous || (previous.end_reason === 'quit_tab_closed' && r.end_reason !== 'quit_tab_closed')) {
      byId[r.run_id] = r;
    }
  });
  return Object.keys(byId).map(function (id) { return byId[id]; }).concat(anonymous);
}

function metricReal(rows) {
  return rows.filter(function (r) { return r.build && !/editor|test|ignore/i.test(String(r.build)); });
}

function metricNumbers(rows, key) {
  return rows.filter(function (r) { return r[key] !== '' && r[key] !== undefined && r[key] !== null; })
    .map(function (r) { return Number(r[key]); }).filter(function (n) { return isFinite(n) && n >= 0; });
}

function metricReports(runRows, tutorialRows) {
  var runs = metricRuns(runRows);
  var deaths = runs.filter(function (r) { return r.end_reason === 'fuel' || r.end_reason === 'police'; });
  var tutorials = dedupe(metricReal(tutorialRows), function (r) {
    return r.ts ? r.session_id + '#' + r.ts + '#' + r.status : '';
  });
  function total(key) { return sum(metricNumbers(deaths, key)); }
  function avg(key) { return mean(metricNumbers(deaths, key)); }
  function count(reason) { return filter(deaths, 'end_reason', reason).length; }
  var base = [['run_id', 'Run ID'], ['ts', 'Timestamp (UTC)'], ['build', 'Build'], ['end_reason', 'Run end']];
  function report(name, summary, fields, rows) {
    return { name: name, summary: summary, fields: fields || [], rows: rows || [] };
  }
  var notes = 'Summary: non-test runs ended by fuel or police. Detail rows also include quits/restarts. Builds are pooled; use Build to compare versions.';
  var reports = [
    report('Overview', [
      ['Recorded non-test runs', runs.length], ['Death-ended runs', deaths.length],
      ['Quits / restarts / other endings', runs.length - deaths.length],
      ['Median survival (seconds)', median(metricNumbers(deaths, 'survival_s'))],
      ['Mean survival (seconds)', avg('survival_s')], ['Mean distance (metres)', avg('distance_m')],
      ['Fuel deaths', count('fuel')], ['Police deaths', count('police')],
      ['Shooting accuracy (total hits / total shots)', pct(total('shots_hit'), total('shots_fired'))],
      ['Tutorial attempts recorded', tutorials.length],
      ['Tutorial completion rate', pct(filter(tutorials, 'status', 'completed').length, tutorials.length)]
    ]),
    report('Survival & Progression', [
      ['Median survival (seconds)', median(metricNumbers(deaths, 'survival_s'))],
      ['Mean distance (metres)', avg('distance_m')],
      ['Runs under 30 seconds', metricNumbers(deaths, 'survival_s').filter(function (s) { return s < 30; }).length],
      ['Runs reaching snow', deaths.filter(function (r) { return truthy(r.reached_snow) || r.reached_snow === 1 || r.reached_snow === '1'; }).length]
    ], base.concat([['survival_s', 'Survival (seconds)'], ['distance_m', 'Distance (metres)'], ['reached_snow', 'Reached snow'], ['city_s', 'City (seconds)'], ['snow_s', 'Snow (seconds)']]), runs),
    report('Shooting & Ammo', [
      ['Shots fired', total('shots_fired')], ['Successful hits', total('shots_hit')],
      ['Pooled accuracy', pct(total('shots_hit'), total('shots_fired'))],
      ['Obstacles destroyed', total('destroyed_green') + total('destroyed_yellow') + total('destroyed_red')],
      ['1-hit obstacles destroyed (Orange)', total('destroyed_green')],
      ['2-hit obstacles destroyed (Red)', total('destroyed_yellow')],
      ['Legacy 3-hit obstacles destroyed', total('destroyed_red')],
      ['Ammo pickups collected', total('ammo_collected')],
      ['Ammo pickups touched while full', total('ammo_wasted_full')]
    ], base.concat([['shots_fired', 'Shots fired'], ['shots_hit', 'Shots hit'], ['accuracy_pct', 'Accuracy (%)'], ['destroyed_green', '1-hit obstacles destroyed (Orange)'], ['destroyed_yellow', '2-hit obstacles destroyed (Red)'], ['destroyed_red', 'Legacy 3-hit obstacles destroyed'], ['ammo_collected', 'Ammo collected'], ['ammo_wasted_full', 'Ammo pickups touched while full'], ['ammo_end', 'Ammo remaining']]), runs),
    report('Collisions & Police', [
      ['Obstacle collisions', total('hits_total')], ['Mean collisions per run', avg('hits_total')],
      ['Police contacts', total('police_contacts')], ['Police deaths', count('police')]
    ], base.concat([['hits_total', 'Obstacle collisions'], ['police_contacts', 'Police contacts'], ['min_police_gap', 'Minimum police gap'], ['hits_shielded', 'Shielded hits'], ['hits_while_reversed', 'Hits while reversed']]), runs),
    report('Fuel', [
      ['Fuel pickups collected', total('fuel_collected')], ['Fuel pickups missed', total('fuel_missed')],
      ['Fuel deaths', count('fuel')], ['Share of deaths caused by fuel', pct(count('fuel'), deaths.length)]
    ], base.concat([['fuel_collected', 'Fuel collected'], ['fuel_missed', 'Fuel missed'], ['min_fuel_pct', 'Minimum fuel (%)'], ['s_below_25_fuel', 'Seconds below 25% fuel'], ['fuel_pct_at_end', 'Fuel at end (%)']]), runs),
    report('Mystery Pickups', [
      ['Mystery boxes collected', total('mystery_collected')], ['Shield outcomes', total('mystery_shield')],
      ['Nitro outcomes', total('mystery_nitro')], ['Police recovery outcomes', total('mystery_recovery')],
      ['Reverse outcomes', total('mystery_reverse')], ['Hits blocked by shield', total('hits_shielded')]
    ], base.concat([['mystery_collected', 'Mystery boxes collected'], ['mystery_shield', 'Shield outcomes'], ['mystery_nitro', 'Nitro outcomes'], ['mystery_recovery', 'Recovery outcomes'], ['mystery_reverse', 'Reverse outcomes'], ['s_reversed', 'Seconds reversed'], ['s_nitro', 'Seconds nitro']]), runs),
    report('Tutorial Summary', [
      ['Recorded attempts', tutorials.length], ['Completed', filter(tutorials, 'status', 'completed').length],
      ['Restarted', filter(tutorials, 'status', 'restarted').length], ['Abandoned', filter(tutorials, 'status', 'abandoned').length],
      ['Completion rate', pct(filter(tutorials, 'status', 'completed').length, tutorials.length)],
      ['Median completed duration (seconds)', median(metricNumbers(filter(tutorials, 'status', 'completed'), 'seconds'))],
      ['Median retries per completed tutorial', median(metricNumbers(filter(tutorials, 'status', 'completed'), 'retries'))]
    ].concat(TUTORIAL_LESSONS.map(function (lesson) {
      // Per lesson: players who passed it, how many on the first try, and the average number of tries.
      var rows = tutorialLessonRows(tutorials, lesson[1]);
      var firstTry = tutorialFirstTryPct(rows, lesson[1]);
      return [lesson[2] + ': passed / first try / mean tries / mean seconds',
        rows.length + ' / ' + (isFinite(firstTry) ? Math.round(firstTry * 10) / 10 + '%' : '') + ' / ' + mean(metricNumbers(rows, lesson[1] + '_attempts')) +
        ' / ' + mean(metricNumbers(rows, lesson[1] + '_s'))];
    })).concat(
      tutorialLogCounts(tutorials, 'fail_log').slice(0, 10).map(function (p) { return ['Failed at ' + p[0], p[1]]; })
    ).concat(
      tutorialCounts(tutorials.filter(function (t) { return t.status !== 'completed'; }), 'quit_step').map(function (p) { return ['Left during ' + p[0], p[1]]; })
    ), [['ts', 'Timestamp (UTC)'], ['session_id', 'Session ID'], ['build', 'Build'], ['status', 'Outcome'], ['seconds', 'Duration (seconds)'],
      ['retries', 'Retries'], ['steps_completed', 'Lessons passed'], ['steps_first_try', 'Lessons passed first try'],
      ['quit_step', 'Exit step'], ['quit_step_attempt', 'Exit step: try number'], ['quit_step_progress', 'Exit step: got past']].concat(
      TUTORIAL_LESSONS.reduce(function (cols, lesson) {
        return cols.concat([[lesson[1] + '_s', lesson[2] + ' (seconds)'], [lesson[1] + '_attempts', lesson[2] + ' (tries)']]);
      }, [])
    ).concat([['fail_log', 'Where they failed'], ['pass_log', 'How they got past each object']]), tutorials)
  ];
  reports.forEach(function (r) {
    r.note = r.name === 'Tutorial Summary' ? 'Non-test recorded tutorial attempts, not unique people. Missing lessons remain blank.' : notes;
    if (r.name === 'Mystery Pickups') r.note += ' Outcome counts describe collection, not necessarily successful use.';
    if (r.name === 'Shooting & Ammo') r.note += ' Destroyed counts use starting strength. Orange/Red are current visual colours for 1-hit/2-hit obstacles; older builds may use different colours. Legacy 3-hit counts remain separate. -1 means not applicable. Ammo pickups touched while full (ammo_wasted_full) can overlap missed pickups; do not add those counts together.';
  });
  return reports;
}

function updateMetricTabs(book) {
  var reports = metricReports(table(book, 'runs'), table(book, 'tutorials'));
  // Avoid replacing someone else's manually created tabs. Check all names before writing.
  reports.forEach(function (r) {
    var sheet = book.getSheetByName(r.name);
    if (sheet && sheet.getLastRow() && sheet.getRange(1, 1).getNote() !== METRIC_TAB_MARKER) {
      throw new Error('Rename the existing "' + r.name + '" tab to preserve it, then run refreshMetricTabs. Raw uploads are saved.');
    }
  });
  var refreshed = new Date().toISOString();
  reports.forEach(function (r) {
    var sheet = book.getSheetByName(r.name) || book.insertSheet(r.name);
    var output = [[r.name, 'Updated ' + refreshed], [r.note], [], ['Metric', 'Value']].concat(r.summary);
    var detailHeader = 0;
    if (r.fields.length) {
      output.push([]);
      output.push(r.fields.map(function (f) { return f[1]; }));
      detailHeader = output.length;
      r.rows.forEach(function (record) {
        output.push(r.fields.map(function (f) { return record[f[0]] === undefined ? '' : record[f[0]]; }));
      });
    }
    var width = Math.max(2, r.fields.length);
    if (sheet.getMaxRows() < output.length) sheet.insertRowsAfter(sheet.getMaxRows(), output.length - sheet.getMaxRows());
    if (sheet.getMaxColumns() < width) sheet.insertColumnsAfter(sheet.getMaxColumns(), width - sheet.getMaxColumns());
    sheet.clearContents();
    sheet.getRange(1, 1).setNote(METRIC_TAB_MARKER);
    sheet.getRange(1, 1, output.length, width).setValues(output.map(function (row) {
      while (row.length < width) row.push('');
      // Treat uploaded text as text, never as spreadsheet formulas.
      return row.map(function (v) { return typeof v === 'string' && v.charAt(0) === '=' ? "'" + v : v; });
    }));
    sheet.getRange(1, 1, 1, width).setBackground('#24476A').setFontColor('#FFFFFF').setFontWeight('bold');
    sheet.getRange(4, 1, 1, 2).setFontWeight('bold');
    if (detailHeader) sheet.getRange(detailHeader, 1, 1, width).setBackground('#DCE7F2').setFontWeight('bold').setWrap(true);
    sheet.setFrozenRows(detailHeader || 4);
    sheet.setColumnWidths(1, width, 170);
    sheet.setColumnWidth(1, 330);
    sheet.getRange(2, 1).setWrap(true);
  });
}

// ---------------------------------------------------------------- helpers

function table(book, name) {
  var sheet = book.getSheetByName(name);
  if (!sheet || sheet.getLastRow() < 2) return [];
  var values = sheet.getDataRange().getValues();
  var headers = values.shift();
  return values.map(function (r) {
    var o = {};
    headers.forEach(function (h, i) { o[h] = r[i]; });
    return o;
  });
}
function dedupe(rows, key) {
  var seen = {};
  return rows.filter(function (r) {
    var k = key(r);
    if (k === undefined || k === '' || k === 'undefined#undefined') return true;
    if (seen[k]) return false;
    seen[k] = true;
    return true;
  });
}
function real(rows) {
  return rows.filter(function (r) { return !/editor|test|ignore/i.test(String(r.build || '')); });
}
function filter(rows, key, value) { return rows.filter(function (r) { return String(r[key]) === String(value); }); }
function unique(rows, key) {
  var seen = {};
  rows.forEach(function (r) { if (r[key] !== '' && r[key] !== undefined) seen[r[key]] = true; });
  return Object.keys(seen);
}
function num(rows, key) {
  return rows.map(function (r) { return Number(r[key]); }).filter(function (n) { return !isNaN(n) && n >= 0; });
}
function sum(xs) { return xs.reduce(function (a, b) { return a + b; }, 0); }
function mean(xs) { return xs.length ? Math.round(sum(xs) / xs.length * 10) / 10 : ''; }
function median(xs) {
  if (!xs.length) return '';
  var s = xs.slice().sort(function (a, b) { return a - b; });
  var m = Math.floor(s.length / 2);
  return s.length % 2 ? s[m] : Math.round((s[m - 1] + s[m]) / 2 * 10) / 10;
}
function pct(a, b) { return b ? Math.round(1000 * a / b) / 10 + '%' : ''; }
function ratio(a, b) { return b ? Math.round(100 * a / b) / 100 : ''; }
function tutorialCounts(rows, key) {
  var c = {};
  rows.forEach(function (r) { var k = r[key] || '(none)'; c[k] = (c[k] || 0) + 1; });
  return Object.keys(c).sort(function (a, b) { return c[b] - c[a]; }).map(function (k) { return [k, c[k]]; });
}

function truthy(v) { return v === true || v === 'true' || v === 'TRUE'; }
function counts(rows, key) {
  var c = {};
  rows.forEach(function (r) { var k = r[key] === '' ? '(blank)' : r[key]; c[k] = (c[k] || 0) + 1; });
  return Object.keys(c).map(function (k) { return k + ': ' + c[k]; }).join(', ');
}
