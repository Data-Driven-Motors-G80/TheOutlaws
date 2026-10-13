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
    if (!row || SHEETS.indexOf(row.sheet) < 0 || typeof row.data !== 'object') return;
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
    return reply({ ok: true, written: written });
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
    .addItem('Refresh dashboard', 'refreshDashboard')
    .addToUi();
}

/** Reads every sheet and writes the key findings to the "dashboard" tab. */
function refreshDashboard() {
  var book = SpreadsheetApp.getActiveSpreadsheet();
  // Editor play ("-editor") and setup/test rows stay in the tabs but are left out of the numbers.
  var runs = real(table(book, 'runs'));
  var events = real(table(book, 'events'));
  var tutorials = real(table(book, 'tutorials'));
  var sessions = real(table(book, 'sessions'));
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
  ['Steer', 'Dodge', 'Fuel', 'Ammo', 'Shoot'].forEach(function (step) {
    var done = filter(filter(events, 'ev', 'tutorial_step_complete'), 'step', step);
    var retries = num(filter(filter(events, 'ev', 'tutorial_retry'), 'step', step), 'attempt').length;
    row('  completed ' + step, pct(unique(done, 'session_id').length, starters),
        'median ' + median(num(done, 'seconds')) + ' s, ' + retries + ' retries total, dry fires ' + sum(num(done, 'dry_fires')));
  });
  var completedTut = filter(tutorials, 'status', 'completed');
  row('Tutorial completion rate', pct(completedTut.length, starters));
  row('Median tutorial length (s)', median(num(completedTut, 'seconds')));
  var abandoned = tutorials.filter(function (t) { return t.status === 'abandoned' || t.status === 'restarted'; });
  row('Tutorials abandoned / restarted', abandoned.length, 'quit step counts: ' + counts(abandoned, 'quit_step'));
  row('Most common retry reasons', counts(filter(events, 'ev', 'tutorial_retry'), 'reason'));

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
function truthy(v) { return v === true || v === 'true' || v === 'TRUE'; }
function counts(rows, key) {
  var c = {};
  rows.forEach(function (r) { var k = r[key] === '' ? '(blank)' : r[key]; c[k] = (c[k] || 0) + 1; });
  return Object.keys(c).map(function (k) { return k + ': ' + c[k]; }).join(', ');
}
