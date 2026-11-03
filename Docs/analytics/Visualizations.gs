/**
 * Wee Woo Run "Visualizations" tabs: pick a start (and optional end) date and time plus which builds
 * to include, and see every metric the game records charted for that range. "WS Visualizations" charts
 * the current WorldShift build (ws_runs / ws_legs / ws_tutorials); "Visualizations" keeps the original
 * endless-runner builds (runs / tutorials / sessions).
 *
 * Add this as a second file next to WeeWooAnalytics.gs (Apps Script editor: + > Script), save, then
 * run setupVisualizations once and authorize. Setup also tidies the workbook (WorldShift tabs first, colours,
 * raw-tab headers) and installs two triggers: editing the filter cells redraws the tab, and a
 * 5-minute timer redraws it when new rows have been uploaded. Raw data is never modified.
 *
 * Counting rules match the metric tabs: runs are de-duplicated by run_id (a normal ending beats a
 * tab-close beacon), "deaths" are runs ended by fuel or police, and -1 / blank values are skipped.
 */

var VIS_TAB = 'Visualizations';
var VIS_MARKERS = ['WeeWoo managed visualizations v1', 'WeeWoo managed visualizations v2'];
var VIS_MARKER = VIS_MARKERS[1];
var VIS_BUILDS = ['All builds (WebGL + editor)', 'WebGL only', 'Editor / test only'];
var VIS_FROM = 'C4', VIS_UNTIL = 'C5', VIS_BUILD = 'C6';
var VIS_COLS = 12, VIS_COL_PX = 110;          // dashboard grid A-L
var VIS_DATA_COL = 14;                        // N onwards: chart source tables
var VIS_FIRST_ROW = 8;                        // everything from here down is redrawn
var VIS_CHART_W = 640, VIS_CHART_H = 330, VIS_CHART_ROWS = 17;
var VIS_NAVY = '#24476A', VIS_INK = '#1F2A37', VIS_MUTED = '#5B6573';
var VIS_PALETTE = ['#2F6DA3', '#D8A35A', '#4C947B', '#C2553A', '#7B6BA8', '#3E9BBF', '#8A8F98'];
var VIS_REPORT_TABS = ['Overview', 'Survival & Progression', 'Shooting & Ammo', 'Collisions & Police', 'Fuel',
  'Mystery Pickups', 'Tutorial Summary'];
var VIS_RAW_TABS = ['runs', 'events', 'tutorials', 'sessions'];
var WS_VIS_TAB = 'WS Visualizations';
var WS_REPORT_TABS = ['WS Overview', 'WS Levels', 'WS Tutorial'];
var WS_RAW_TABS = ['ws_runs', 'ws_legs', 'ws_tutorials'];
// Each dashboard: its raw tabs, the tabs its default start date comes from, its default build filter and its metrics.
var VIS_DASHBOARDS = {};
VIS_DASHBOARDS[WS_VIS_TAB] = { title: 'Wee Woo Run  ·  WorldShift analytics', raw: WS_RAW_TABS,
  dated: ['ws_runs', 'ws_tutorials'], builds: VIS_BUILDS[1], compute: function (d, o, tz) { return wsVizCompute(d, o, tz); } };
VIS_DASHBOARDS[VIS_TAB] = { title: 'Wee Woo Run  ·  Original endless runner', raw: VIS_RAW_TABS,
  dated: ['runs', 'sessions'], builds: VIS_BUILDS[0], compute: function (d, o, tz) { return vizCompute(d, o, tz); } };

// ---------------------------------------------------------------- setup and triggers

function setupVisualizations() {
  var book = SpreadsheetApp.getActiveSpreadsheet();
  [WS_VIS_TAB, VIS_TAB].forEach(function (name) {
    var sheet = book.getSheetByName(name);
    if (sheet && sheet.getLastRow() && VIS_MARKERS.indexOf(sheet.getRange(1, 1).getNote()) < 0) {
      throw new Error('Rename the existing "' + name + '" tab to preserve it, then run setupVisualizations again.');
    }
  });
  var first = vizSetupTab(book, WS_VIS_TAB);
  vizSetupTab(book, VIS_TAB);
  vizInstallTriggers(book);
  vizStyleWorkbook(book);
  refreshVisualizations();
  book.setActiveSheet(first);
}

/** Lays out one dashboard's title band and filter cells; charts are drawn by refreshVisualizations. */
function vizSetupTab(book, name) {
  var dashboard = VIS_DASHBOARDS[name];
  var sheet = book.getSheetByName(name) || book.insertSheet(name, 0);
  sheet.getCharts().forEach(function (chart) { sheet.removeChart(chart); });
  vizEnsureSize(sheet, 60, VIS_DATA_COL + 6);
  sheet.getRange(1, 1, sheet.getMaxRows(), sheet.getMaxColumns()).breakApart().clear().clearDataValidations();
  sheet.setHiddenGridlines(true);
  sheet.setColumnWidths(1, VIS_COLS, VIS_COL_PX);
  sheet.setColumnWidth(VIS_COLS + 1, 40);

  sheet.getRange(1, 1, 1, VIS_COLS).merge().setValue(dashboard.title)
    .setFontSize(20).setFontWeight('bold').setFontColor('#FFFFFF').setBackground(VIS_NAVY).setVerticalAlignment('middle');
  sheet.getRange(1, 1).setNote(VIS_MARKER);
  sheet.setRowHeight(1, 46);
  sheet.getRange(2, 1, 1, VIS_COLS).merge().setFontColor('#DCE7F2').setBackground(VIS_NAVY).setFontSize(10);
  sheet.setRowHeight(3, 10);

  var filters = [
    [4, 'Show runs from', firstUploadDate(book, dashboard.dated), 'Double-click for a calendar, then type a time if you want one (e.g. 10/19/2026 2:30 PM).'],
    [5, 'Until', '', 'Leave blank for "now". Same format as above.'],
    [6, 'Builds', dashboard.builds, 'Editor runs are tagged "-editor"; WebGL only is what real players produced.']
  ];
  filters.forEach(function (f) {
    sheet.getRange(f[0], 1, 1, 2).merge().setValue(f[1]).setFontWeight('bold').setFontColor(VIS_INK)
      .setHorizontalAlignment('right').setVerticalAlignment('middle');
    var input = sheet.getRange(f[0], 3, 1, 3).merge().setValue(f[2]).setFontWeight('bold').setBackground('#FFF6D6')
      .setBorder(true, true, true, true, false, false, '#E0C46C', SpreadsheetApp.BorderStyle.SOLID).setVerticalAlignment('middle');
    if (f[0] === 6) {
      input.setDataValidation(SpreadsheetApp.newDataValidation().requireValueInList(VIS_BUILDS, true).build());
    } else {
      input.setNumberFormat('ddd mmm d, yyyy  h:mm am/pm').setDataValidation(SpreadsheetApp.newDataValidation()
        .requireDate().setAllowInvalid(true).setHelpText('Pick a date, optionally followed by a time.').build());
    }
    sheet.getRange(f[0], 6, 1, VIS_COLS - 5).merge().setValue(f[3]).setFontColor(VIS_MUTED).setFontSize(9)
      .setVerticalAlignment('middle');
    sheet.setRowHeight(f[0], 28);
  });
  sheet.setRowHeight(7, 10);
  return sheet;
}

function vizInstallTriggers(book) {
  ScriptApp.getProjectTriggers().forEach(function (trigger) {
    var handler = trigger.getHandlerFunction();
    if (handler === 'onVisualizationsEdit' || handler === 'refreshVisualizationsIfChanged') ScriptApp.deleteTrigger(trigger);
  });
  ScriptApp.newTrigger('onVisualizationsEdit').forSpreadsheet(book).onEdit().create();
  ScriptApp.newTrigger('refreshVisualizationsIfChanged').timeBased().everyMinutes(5).create();
}

/** Installable edit trigger: redraw when one of the three filter cells changes. */
function onVisualizationsEdit(e) {
  var range = e && e.range;
  if (!range || !VIS_DASHBOARDS[range.getSheet().getName()]) return;
  if (range.getLastRow() < 4 || range.getRow() > 6 || range.getColumn() > 5) return;
  refreshVisualizations(range.getSheet().getName());
}

/** Timer trigger: redraw only the dashboards whose raw tabs gained rows since their last redraw. */
function refreshVisualizationsIfChanged() {
  var book = SpreadsheetApp.getActiveSpreadsheet();
  var props = PropertiesService.getDocumentProperties();
  Object.keys(VIS_DASHBOARDS).forEach(function (name) {
    if (props.getProperty('vizDataKey:' + name) !== vizDataKey(book, VIS_DASHBOARDS[name].raw)) refreshVisualizations(name);
  });
}

function vizDataKey(book, tabs) {
  return tabs.map(function (name) {
    var sheet = book.getSheetByName(name);
    return sheet ? sheet.getLastRow() : 0;
  }).join(',');
}

/** Earliest upload of any build, at midnight, so the default view shows everything. */
function firstUploadDate(book, tabs) {
  var first = null;
  (tabs || ['runs', 'sessions']).forEach(function (name) {
    table(book, name).forEach(function (r) {
      var t = vizTime(r.ts);
      if (t !== null && (first === null || t < first)) first = t;
    });
  });
  var date = first === null ? new Date() : new Date(first);
  date.setHours(0, 0, 0, 0);
  return date;
}

// ---------------------------------------------------------------- redraw

/** Redraws one dashboard by tab name, or both when no name is given. */
function refreshVisualizations(only) {
  var book = SpreadsheetApp.getActiveSpreadsheet();
  // A document lock, not the script lock: redraws only read raw tabs and must never make uploads wait.
  var lock = LockService.getDocumentLock();
  lock.waitLock(25000);
  try {
    var tz = book.getSpreadsheetTimeZone();
    Object.keys(VIS_DASHBOARDS).forEach(function (name) {
      var sheet = book.getSheetByName(name);
      if (!sheet || (typeof only === 'string' && only !== name)) return;
      var dashboard = VIS_DASHBOARDS[name], opts = vizReadOptions(sheet), data = {};
      var key = vizDataKey(book, dashboard.raw);
      dashboard.raw.forEach(function (tab) { data[tab] = table(book, tab); });
      vizRender(sheet, dashboard.compute(data, opts, tz), opts, tz);
      PropertiesService.getDocumentProperties().setProperty('vizDataKey:' + name, key);
    });
  } finally {
    lock.releaseLock();
  }
}

function vizReadOptions(sheet) {
  var warnings = [];
  function time(a1, label) {
    var v = sheet.getRange(a1).getValue();
    if (v instanceof Date) return v.getTime();
    if (v === '' || v === null) return null;
    var t = Date.parse(String(v));
    if (isNaN(t)) { warnings.push('"' + label + '" is not a date, so it is ignored'); return null; }
    return t;
  }
  var builds = String(sheet.getRange(VIS_BUILD).getValue() || VIS_BUILDS[0]);
  return { from: time(VIS_FROM, 'Show runs from'), until: time(VIS_UNTIL, 'Until'),
    builds: VIS_BUILDS.indexOf(builds) < 0 ? VIS_BUILDS[0] : builds, warnings: warnings };
}

function vizRender(sheet, result, opts, tz) {
  sheet.getCharts().forEach(function (chart) { sheet.removeChart(chart); });

  // Plan the layout first so the sheet can be sized once.
  var kpiRow = VIS_FIRST_ROW + 1;
  var kpiBlocks = Math.ceil(result.kpis.length / 6);
  var linkRow = kpiRow + kpiBlocks * 3;
  var linkRows = Math.ceil(result.sections.length / 6);
  var row = linkRow + linkRows + 1;
  result.sections.forEach(function (section) {
    section.row = row;
    row += 1 + Math.ceil(section.charts.length / 2) * VIS_CHART_ROWS + 1;
  });
  var dataRows = 2;
  result.sections.forEach(function (s) { s.charts.forEach(function (c) { dataRows += vizRows(c).length + 3; }); });
  vizEnsureSize(sheet, Math.max(row, VIS_FIRST_ROW + dataRows) + 2, VIS_DATA_COL + 6);

  var below = sheet.getRange(VIS_FIRST_ROW, 1, sheet.getMaxRows() - VIS_FIRST_ROW + 1, sheet.getMaxColumns());
  below.breakApart().clear().clearDataValidations();
  sheet.setRowHeights(VIS_FIRST_ROW, sheet.getMaxRows() - VIS_FIRST_ROW + 1, 21);

  // Status line under the title.
  var fmt = 'MMM d, yyyy h:mm a';
  var status = 'Showing ' + (result.counts.label || result.counts.runs + ' runs (' + result.counts.deaths + ' ended by fuel or police)') + ' from ' +
    (opts.from ? Utilities.formatDate(new Date(opts.from), tz, fmt) : 'the first upload') + ' to ' +
    (opts.until ? Utilities.formatDate(new Date(opts.until), tz, fmt) : 'now') + '  ·  ' + opts.builds +
    '  ·  times in ' + tz + '  ·  refreshed ' + Utilities.formatDate(new Date(), tz, fmt);
  if (opts.warnings.length) status += '  ·  ⚠ ' + opts.warnings.join('; ');
  sheet.getRange(2, 1).setValue(status);

  // Headline number cards, six per row.
  result.kpis.forEach(function (kpi, i) {
    var r = kpiRow + Math.floor(i / 6) * 3, c = 1 + (i % 6) * 2;
    sheet.getRange(r, c, 1, 2).merge().setValue(kpi[0]).setFontSize(9).setFontColor(VIS_MUTED)
      .setBackground('#F1F5F9').setVerticalAlignment('bottom');
    // Plain text first, so "61.4%" is not reparsed as a number and shown as "61.40%".
    sheet.getRange(r + 1, c, 1, 2).merge().setNumberFormat('@').setValue(kpi[1]).setFontSize(18).setFontWeight('bold')
      .setFontColor(VIS_INK).setBackground('#F1F5F9').setHorizontalAlignment('left');
    sheet.getRange(r, c, 2, 2).setBorder(true, true, true, true, false, false, '#FFFFFF', SpreadsheetApp.BorderStyle.SOLID_THICK);
    sheet.setRowHeight(r + 1, 34);
  });

  // "Jump to" links, then each section's header band and its charts in a two-column grid.
  var gid = sheet.getSheetId();
  result.sections.forEach(function (section, i) {
    var r = linkRow + Math.floor(i / 6), c = 1 + (i % 6) * 2;
    sheet.getRange(r, c, 1, 2).merge().setFormula('=HYPERLINK("#gid=' + gid + '&range=A' + section.row + '","' +
      section.title.replace(/"/g, '') + ' ›")').setFontColor('#2F6DA3').setFontSize(10);
  });

  var dataRow = VIS_FIRST_ROW;
  sheet.getRange(dataRow, VIS_DATA_COL, 1, 6).merge().setValue('Chart data (generated for the charts; edits are overwritten)')
    .setFontWeight('bold').setFontColor('#FFFFFF').setBackground(VIS_MUTED);
  dataRow += 2;
  result.sections.forEach(function (section) {
    sheet.getRange(section.row, 1, 1, VIS_COLS).merge()
      .setValue(section.title + (section.subtitle ? '   —   ' + section.subtitle : ''))
      .setFontSize(13).setFontWeight('bold').setFontColor('#FFFFFF').setBackground(VIS_NAVY).setVerticalAlignment('middle');
    sheet.setRowHeight(section.row, 32);
    section.charts.forEach(function (spec, j) {
      var rows = vizRows(spec);
      sheet.getRange(dataRow, VIS_DATA_COL).setValue(spec.title).setFontWeight('bold').setFontColor(VIS_INK);
      var range = sheet.getRange(dataRow + 1, VIS_DATA_COL, rows.length + 1, spec.header.length);
      // Category labels such as "0" or "1" must stay text, or the chart treats them as a data series.
      if (spec.type !== 'scatter' && spec.type !== 'line') range.offset(0, 0, rows.length + 1, 1).setNumberFormat('@');
      range.setValues([spec.header].concat(rows));
      sheet.getRange(dataRow + 1, VIS_DATA_COL, 1, spec.header.length).setFontWeight('bold').setBackground('#DCE7F2');
      dataRow += rows.length + 3;
      vizInsertChart(sheet, spec, range, section.row + 1 + Math.floor(j / 2) * VIS_CHART_ROWS, j % 2 ? 7 : 1);
    });
  });
}

/** Chart rows, with a placeholder so empty charts still render. */
function vizRows(spec) {
  if (spec.rows.length) return spec.rows;
  var empty = spec.type === 'scatter' || spec.type === 'line' ? [0] : ['No data in this range'];
  while (empty.length < spec.header.length) empty.push(0);
  return [empty];
}

function vizInsertChart(sheet, spec, range, row, col) {
  var types = { column: Charts.ChartType.COLUMN, stacked: Charts.ChartType.COLUMN, bar: Charts.ChartType.BAR,
    pie: Charts.ChartType.PIE, line: Charts.ChartType.LINE, scatter: Charts.ChartType.SCATTER };
  function base() {
    return sheet.newChart().setChartType(types[spec.type]).addRange(range).setNumHeaders(1).setPosition(row, col, 0, 0)
      .setOption('title', spec.title).setOption('width', VIS_CHART_W).setOption('height', VIS_CHART_H);
  }
  var chart = base()
    .setOption('colors', spec.colors || VIS_PALETTE)
    .setOption('titleTextStyle', { fontSize: 14, bold: true, color: VIS_INK })
    .setOption('legend', { position: spec.header.length === 2 && spec.type !== 'pie' ? 'none' : 'bottom' });
  var x = { title: spec.xTitle || spec.header[0] }, y = { title: spec.yTitle || spec.header[1], minValue: 0 };
  if (spec.type === 'pie') chart.setOption('pieHole', 0.45).setOption('pieSliceText', 'value');
  else if (spec.type === 'bar') chart.setOption('hAxis', { title: spec.yTitle || '', minValue: 0 });
  else chart.setOption('hAxis', x).setOption('vAxis', y);
  if (spec.type === 'stacked') chart.setOption('isStacked', true);
  if (spec.type === 'scatter') chart.setOption('pointSize', 6);
  if (spec.type === 'line') chart.setOption('pointSize', 5);
  if (spec.trend) chart.setOption('trendlines', { 0: { color: '#C2553A', lineWidth: 2, opacity: 0.7 } });
  try {
    sheet.insertChart(chart.build());
  } catch (err) {
    // An option this Sheets version rejects should never cost us the chart itself.
    console.warn('Chart "' + spec.title + '" fell back to defaults: ' + err);
    sheet.insertChart(base().build());
  }
}

function vizEnsureSize(sheet, rows, cols) {
  if (sheet.getMaxRows() < rows) sheet.insertRowsAfter(sheet.getMaxRows(), rows - sheet.getMaxRows());
  if (sheet.getMaxColumns() < cols) sheet.insertColumnsAfter(sheet.getMaxColumns(), cols - sheet.getMaxColumns());
}

/** Orders and colours the tabs and styles raw-tab headers. Only formatting changes; no values. */
function vizStyleWorkbook(book) {
  // The current WorldShift build comes first; the original runner's tabs follow, then raw data.
  var order = [WS_VIS_TAB].concat(WS_REPORT_TABS, [VIS_TAB], VIS_REPORT_TABS, ['dashboard'], WS_RAW_TABS, VIS_RAW_TABS);
  var position = 1;
  order.forEach(function (name) {
    var sheet = book.getSheetByName(name);
    if (!sheet) return;
    book.setActiveSheet(sheet);
    book.moveActiveSheet(position++);
    sheet.setTabColor(name === WS_VIS_TAB ? '#C2553A' : WS_REPORT_TABS.indexOf(name) >= 0 ? '#D8A35A'
      : name === VIS_TAB ? VIS_NAVY : VIS_REPORT_TABS.indexOf(name) >= 0 ? '#4C947B' : name === 'dashboard' ? '#D8A35A' : '#8A8F98');
  });
  VIS_RAW_TABS.concat(WS_RAW_TABS).forEach(function (name) {
    var sheet = book.getSheetByName(name);
    if (!sheet || !sheet.getLastColumn()) return;
    sheet.getRange(1, 1, 1, sheet.getMaxColumns()).setBackground('#33475B').setFontColor('#FFFFFF').setFontWeight('bold');
    sheet.setFrozenRows(1);
  });
}

// ---------------------------------------------------------------- metrics (pure: no Sheets calls)

function vizTime(v) {
  if (v instanceof Date) return v.getTime();
  if (v === '' || v === null || v === undefined) return null;
  var t = Date.parse(String(v));
  return isNaN(t) ? null : t;
}

function vizNums(rows, key) {
  var out = [];
  rows.forEach(function (r) {
    var v = r[key];
    if (v === '' || v === null || v === undefined) return;
    var n = Number(v);
    if (isFinite(n) && n >= 0) out.push(n);
  });
  return out;
}
function vizSum(rows, key) { return sum(vizNums(rows, key)); }
function vizMean(rows, key) { return vizAvg(vizNums(rows, key)); }
function vizAvg(xs) { return xs.length ? Math.round(sum(xs) / xs.length * 10) / 10 : 0; }
function vizPct(a, b) { return b ? Math.round(1000 * a / b) / 10 : 0; }
function vizMax(xs) { return xs.length ? Math.max.apply(null, xs) : 0; }

/** [[label, count]] sorted by count, using friendly labels where given. */
function vizCount(rows, key, labels) {
  var c = {};
  rows.forEach(function (r) {
    var k = r[key] === '' || r[key] === undefined || r[key] === null ? '(blank)' : String(r[key]);
    c[k] = (c[k] || 0) + 1;
  });
  return Object.keys(c).sort(function (a, b) { return c[b] - c[a]; })
    .map(function (k) { return [(labels && labels[k]) || k, c[k]]; });
}

/** Histogram rows for edges like [0, 30, 60]; the last bin is open-ended. */
function vizBins(values, edges, label) {
  return edges.map(function (lo, i) {
    var hi = i + 1 < edges.length ? edges[i + 1] : null;
    return [label(lo, hi), values.filter(function (v) { return v >= lo && (hi === null || v < hi); }).length];
  });
}
function vizSecondsLabel(lo, hi) { return hi === null ? lo + 's+' : lo + '–' + hi + 's'; }
function vizCountLabel(lo, hi) { return hi === null ? lo + '+' : hi - lo === 1 ? String(lo) : lo + '–' + (hi - 1); }

function vizDay(t, tz) {
  if (typeof Utilities !== 'undefined') return Utilities.formatDate(new Date(t), tz, 'yyyy-MM-dd|EEE MMM d');
  return new Date(t).toISOString().substring(0, 10) + '|' + new Date(t).toISOString().substring(5, 10);
}

function vizCompute(data, opts, tz) {
  function keep(r) {
    var t = vizTime(r.ts);
    if (t === null || (opts.from && t < opts.from) || (opts.until && t > opts.until)) return false;
    var build = String(r.build || ''), editor = /editor|test|ignore/i.test(build);
    if (opts.builds === VIS_BUILDS[1]) return !!build && !editor;
    if (opts.builds === VIS_BUILDS[2]) return editor;
    return true;
  }
  function byTime(a, b) { return vizTime(a.ts) - vizTime(b.ts); }

  var byId = {}, runs = [];
  data.runs.filter(keep).forEach(function (r) {
    if (!r.run_id) { runs.push(r); return; }
    var previous = byId[r.run_id];
    if (!previous || (previous.end_reason === 'quit_tab_closed' && r.end_reason !== 'quit_tab_closed')) byId[r.run_id] = r;
  });
  runs = runs.concat(Object.keys(byId).map(function (id) { return byId[id]; })).sort(byTime);
  var deaths = runs.filter(function (r) { return r.end_reason === 'fuel' || r.end_reason === 'police'; });
  var events = dedupe(data.events.filter(keep), function (r) { return r.session_id + '#' + r.seq; });
  var tutorials = dedupe(data.tutorials.filter(keep), function (r) { return r.session_id + '#' + r.ts + '#' + r.status; });
  var sessions = dedupe(data.sessions.filter(keep).sort(byTime).reverse(), function (r) { return r.session_id; });
  function ev(name) { return events.filter(function (e) { return e.ev === name; }); }

  var survival = vizNums(deaths, 'survival_s');
  var distance = vizNums(deaths, 'distance_m');
  var shots = vizSum(deaths, 'shots_fired'), hits = vizSum(deaths, 'shots_hit');
  var kinds = [['fuel', 'Fuel'], ['ammo', 'Ammo'], ['mystery', 'Mystery box']];
  var collected = 0, missed = 0;
  kinds.forEach(function (k) { collected += vizSum(deaths, k[0] + '_collected'); missed += vizSum(deaths, k[0] + '_missed'); });
  var players = {};
  runs.concat(events).forEach(function (r) { if (r.player_id) players[r.player_id] = true; });
  var completedTutorials = filter(tutorials, 'status', 'completed').length;

  var kpis = [
    ['Runs recorded', String(runs.length)],
    ['Ended by fuel or police', String(deaths.length)],
    ['Players (browsers)', String(Object.keys(players).length)],
    ['Sessions', String(unique(events, 'session_id').length)],
    ['Median survival', (median(survival) || 0) + ' s'],
    ['Longest survival', vizMax(survival) + ' s'],
    ['Mean distance', Math.round(vizAvg(distance)) + ' m'],
    ['Best distance', vizMax(distance) + ' m'],
    ['Shooting accuracy', vizPct(hits, shots) + '%'],
    ['Pickups collected', vizPct(collected, collected + missed) + '%'],
    ['Tutorials completed', completedTutorials + ' of ' + tutorials.length],
    ['Missions completed', String(vizSum(runs, 'missions_completed_count'))]
  ];

  function chart(title, type, header, rows, extra) {
    var spec = { title: title, type: type, header: header, rows: rows };
    Object.keys(extra || {}).forEach(function (k) { spec[k] = extra[k]; });
    return spec;
  }
  function percentReaching(values, step) {
    var rows = [], top = Math.ceil(vizMax(values) / step) * step;
    for (var t = 0; t <= top; t += step) {
      rows.push([t, vizPct(values.filter(function (v) { return v >= t; }).length, values.length)]);
    }
    return values.length ? rows : [];
  }
  function groupMean(rows, groupOf, key) {
    var groups = {}, order = [];
    rows.forEach(function (r) {
      var g = groupOf(r);
      if (g === null) return;
      if (!groups[g]) { groups[g] = []; order.push(g); }
      groups[g].push(r);
    });
    return order.map(function (g) { return [g + ' (' + groups[g].length + (groups[g].length === 1 ? ' run)' : ' runs)'), vizMean(groups[g], key)]; });
  }

  // Survival
  var learning = groupMean(deaths.slice().sort(function (a, b) {
    return Number(a.run_number_session) - Number(b.run_number_session);
  }), function (r) {
    var n = Number(r.run_number_session);
    return !n ? null : n >= 5 ? 'Run 5+' : 'Run ' + n;
  }, 'survival_s');
  var survivalSection = { title: 'Survival', subtitle: 'how long runs last (GDD metric 1)', charts: [
    chart('Survival time per run', 'column', ['Survival time', 'Runs'],
      vizBins(survival, [0, 30, 60, 90, 120, 180, 240, 300], vizSecondsLabel), { yTitle: 'Runs' }),
    chart('Survival curve: % of runs still going', 'line', ['Seconds survived', '% of runs still going'],
      percentReaching(survival, vizMax(survival) > 300 ? 30 : 15)),
    chart('Distance curve: % of runs reaching each distance', 'line', ['Metres', '% of runs reaching'],
      percentReaching(distance, 250)),
    chart('Survival per run, in the order played', 'scatter', ['Run # (oldest first)', 'Survival (s)'],
      deaths.map(function (r, i) { return [i + 1, Number(r.survival_s) || 0]; }), { trend: true }),
    chart('Learning curve: mean survival by run # in a session', 'column', ['Run in session', 'Mean survival (s)'], learning),
    chart('Tutorial on vs off: mean survival', 'column', ['Tutorial', 'Mean survival (s)'], groupMean(deaths, function (r) {
      return truthy(r.tutorial_was_on) ? 'Tutorial on' : 'Tutorial off';
    }, 'survival_s')),
    chart('Mean survival by build', 'bar', ['Build', 'Mean survival (s)'],
      groupMean(deaths, function (r) { return String(r.build || '(none)'); }, 'survival_s'), { yTitle: 'Seconds' })
  ]};

  // Cause of death
  var endLabels = { fuel: 'Out of fuel', police: 'Caught by police', restart: 'Restarted', restart_from_pause: 'Restarted from pause',
    quit_tab_closed: 'Closed the tab mid-run' };
  var causeLabels = { fuel_missed_tanks: 'Fuel: drove past a tank', fuel_red_block_hits: 'Fuel: hit red blocks', fuel_other: 'Fuel: other',
    police_right_after_hit: 'Police: within 5 s of a hit', police_after_many_hits: 'Police: after 3+ hits', police_slow_pressure: 'Police: slow pressure' };
  function share(test) { return vizPct(deaths.filter(test).length, deaths.length); }
  var deathSection = { title: 'Cause of death', subtitle: 'what ends runs (GDD metric 2)', charts: [
    chart('Cause of death', 'pie', ['Cause', 'Runs'], [['Out of fuel', filter(deaths, 'end_reason', 'fuel').length],
      ['Caught by police', filter(deaths, 'end_reason', 'police').length]], { colors: ['#D8A35A', '#2F6DA3'] }),
    chart('How every run ended (incl. quits and restarts)', 'bar', ['Ending', 'Runs'], vizCount(runs, 'end_reason', endLabels), { yTitle: 'Runs' }),
    chart('Cause of death in detail', 'bar', ['Cause', 'Runs'], vizCount(deaths, 'death_cause', causeLabels), { yTitle: 'Runs' }),
    chart('Situation at the moment of death (% of deaths)', 'bar', ['Situation', '% of deaths'], [
      ['Still holding ammo', share(function (r) { return Number(r.ammo_at_end) > 0; })],
      ['2+ hits in the last 10 s', share(function (r) { return Number(r.hits_last_10s) >= 2; })],
      ['Fuel below 25%', share(function (r) { return r.fuel_pct_at_end !== '' && Number(r.fuel_pct_at_end) < 25; })],
      ['Shield ready', share(function (r) { return truthy(r.shield_at_end); })],
      ['Controls reversed', share(function (r) { return truthy(r.reversed_at_end); })],
      ['Nitro active', share(function (r) { return truthy(r.nitro_at_end); })]
    ], { yTitle: '% of deaths' })
  ]};

  // Shooting
  var checkpoints = {}, marks = [];
  ev('checkpoint').forEach(function (e) {
    var m = Number(e.m);
    if (!m) return;
    if (!checkpoints[m]) { checkpoints[m] = []; marks.push(m); }
    checkpoints[m].push(e);
  });
  marks.sort(function (a, b) { return a - b; });
  var collisions = ev('collision');
  var couldShoot = collisions.filter(function (e) { return truthy(e.could_shoot); }).length;
  var full = vizSum(deaths, 's_ammo_full'), empty = vizSum(deaths, 's_ammo_empty');
  var shootingSection = { title: 'Shooting', subtitle: 'accuracy and ammo use (GDD metric 3)', charts: [
    chart('Shooting accuracy: shots that hit an obstacle', 'pie', ['Shot', 'Shots'], [['Hit', hits], ['Missed', Math.max(0, shots - hits)]],
      { colors: ['#4C947B', '#C9D1D9'] }),
    chart('Accuracy per run, in the order played', 'scatter', ['Run # (oldest first)', 'Accuracy (%)'],
      deaths.filter(function (r) { return Number(r.shots_fired) > 0; })
        .map(function (r, i) { return [i + 1, Math.round(100 * Number(r.shots_hit) / Number(r.shots_fired))]; }), { trend: true }),
    chart('Shots fired per run', 'column', ['Shots fired', 'Runs'], vizBins(vizNums(deaths, 'shots_fired'), [0, 1, 6, 11, 21, 41], vizCountLabel)),
    chart('Ammo level across all driving time', 'pie', ['Ammo', 'Seconds'], [['Full (5/5)', Math.round(full)], ['Empty', Math.round(empty)],
      ['Some ammo', Math.max(0, Math.round(sum(survival) - full - empty))]], { colors: ['#2F6DA3', '#C2553A', '#C9D1D9'] }),
    chart('Per 250 m of road: shots, kills, dodges and hits (average)', 'line',
      ['Distance (m)', 'Shots fired', 'Obstacles destroyed', 'Obstacles dodged', 'Collisions'],
      marks.map(function (m) {
        var at = checkpoints[m];
        return [m, vizMean(at, 'shots_since'), vizMean(at, 'destroyed_since'), vizMean(at, 'dodged_since'), vizMean(at, 'hits_since')];
      })),
    chart('Collisions: did the player have enough ammo to shoot it?', 'pie', ['Had ammo', 'Collisions'],
      collisions.length ? [['Yes, but did not shoot it', couldShoot], ['No, not enough ammo', collisions.length - couldShoot]] : [],
      { colors: ['#D8A35A', '#8A8F98'] }),
    chart('Ammo mistakes (totals)', 'bar', ['Mistake', 'Count'], [
      ['Dry fires (Space with no ammo)', vizSum(deaths, 'dry_fires')],
      ['Ammo boxes touched while full', vizSum(deaths, 'ammo_wasted_full')],
      ['Shots that hit nothing', vizSum(deaths, 'shots_missed')]
    ], { yTitle: 'Count' })
  ]};

  // Pickups
  var pickupSection = { title: 'Pickups', subtitle: 'what players collect (GDD metric 4)', charts: [
    chart('Pickups collected vs missed', 'column', ['Pickup', 'Collected', 'Missed'],
      kinds.map(function (k) { return [k[1], vizSum(deaths, k[0] + '_collected'), vizSum(deaths, k[0] + '_missed')]; }),
      { colors: ['#4C947B', '#D8A35A'], yTitle: 'Pickups' }),
    chart('Collection rate (% of pickups that appeared)', 'column', ['Pickup', '% collected'],
      kinds.map(function (k) { return [k[1], vizPct(vizSum(deaths, k[0] + '_collected'), vizSum(deaths, k[0] + '_spawned'))]; })),
    chart('Mystery box outcomes', 'pie', ['Outcome', 'Times'], [['Shield', vizSum(deaths, 'mystery_shield')],
      ['Nitro', vizSum(deaths, 'mystery_nitro')], ['Police pushed back', vizSum(deaths, 'mystery_recovery')],
      ['Reversed steering', vizSum(deaths, 'mystery_reverse')], ['Sunny day', vizSum(deaths, 'mystery_sunny')]]
      .filter(function (r) { return r[1] > 0; })),
    chart('Fuel tanks collected vs survival', 'scatter', ['Fuel tanks collected in the run', 'Survival (s)'],
      deaths.map(function (r) { return [Number(r.fuel_collected) || 0, Number(r.survival_s) || 0]; }), { trend: true })
  ]};

  // Obstacles and collisions
  var colours = [['green', '1-hit obstacles (orange)'], ['yellow', '2-hit obstacles (red)'], ['red', 'Legacy 3-hit obstacles']];
  var passed = colours.map(function (c) {
    return [c[1], vizSum(deaths, 'dodged_' + c[0]), vizSum(deaths, 'hits_' + c[0]), vizSum(deaths, 'destroyed_' + c[0])];
  }).filter(function (r) { return r[1] + r[2] + r[3] > 0; });
  var obstacleSection = { title: 'Obstacles & collisions', subtitle: 'how players get past blocks', charts: [
    chart('How obstacles were passed', 'stacked', ['Obstacle', 'Dodged', 'Hit', 'Shot down'], passed,
      { colors: ['#4C947B', '#C2553A', '#2F6DA3'], yTitle: 'Obstacles' }),
    chart('Obstacle collisions per run', 'column', ['Collisions', 'Runs'], vizBins(vizNums(deaths, 'hits_total'), [0, 1, 3, 6, 11], vizCountLabel)),
    chart('Collisions vs survival', 'scatter', ['Collisions in the run', 'Survival (s)'],
      deaths.map(function (r) { return [Number(r.hits_total) || 0, Number(r.survival_s) || 0]; }), { trend: true }),
    chart('Special collisions (totals)', 'bar', ['Collision', 'Count'], [
      ['Absorbed by a shield', vizSum(deaths, 'hits_shielded')], ['While steering was reversed', vizSum(deaths, 'hits_while_reversed')],
      ['While nitro was active', vizSum(deaths, 'hits_with_nitro')], ['Police contacts', vizSum(deaths, 'police_contacts')]
    ], { yTitle: 'Count' })
  ]};

  // Power-ups and progression
  var missionCounts = vizCount(ev('mission_complete'), 'mission');
  var progressSection = { title: 'Power-ups & progression', subtitle: 'effects, snow and missions', charts: [
    chart('Average seconds per run with each effect', 'bar', ['Effect', 'Seconds per run'], [
      ['Shield ready', vizMean(deaths, 's_shield_ready')], ['Nitro', vizMean(deaths, 's_nitro')],
      ['Reversed steering', vizMean(deaths, 's_reversed')], ['Fuel below 25%', vizMean(deaths, 's_below_25_fuel')]
    ], { yTitle: 'Seconds' }),
    chart('Runs that reached the snow', 'pie', ['Reached snow', 'Runs'], [['Reached snow', deaths.filter(function (r) {
      return truthy(r.reached_snow); }).length], ['Stayed in the city', deaths.filter(function (r) { return !truthy(r.reached_snow); }).length]],
      { colors: ['#3E9BBF', '#C9D1D9'] }),
    chart('Missions completed per run', 'column', ['Missions completed', 'Runs'],
      vizBins(vizNums(runs, 'missions_completed_count'), [0, 1, 2, 3, 4], vizCountLabel)),
    chart('Mission completions', 'bar', ['Mission', 'Completions'], missionCounts, { yTitle: 'Completions' })
  ]};

  // Tutorial
  var lessons = TUTORIAL_LESSONS.filter(function (l) { return tutorialLessonRows(tutorials, l[1]).length; });
  var quits = tutorials.filter(function (t) { return t.status === 'abandoned' || t.status === 'restarted'; });
  var lessonLabels = {};
  TUTORIAL_LESSONS.forEach(function (l) { lessonLabels[l[0]] = l[2]; });
  var tutorialSection = { title: 'Tutorial', subtitle: 'completion, tries per lesson and where players fail', charts: [
    chart('Tutorial outcomes', 'pie', ['Outcome', 'Attempts'], vizCount(tutorials, 'status'), { colors: ['#4C947B', '#C2553A', '#D8A35A'] }),
    chart('Average seconds per lesson', 'bar', ['Lesson', 'Seconds'],
      lessons.map(function (l) { return [l[2], vizMean(tutorials, l[1] + '_s')]; }), { yTitle: 'Seconds' }),
    chart('Average tries per lesson', 'bar', ['Lesson', 'Tries'],
      lessons.map(function (l) { return [l[2], vizMean(tutorials, l[1] + '_attempts')]; }), { yTitle: 'Tries' }),
    chart('Passed on the first try (%)', 'bar', ['Lesson', '% first try'],
      lessons.map(function (l) {
        var firstTry = tutorialFirstTryPct(tutorialLessonRows(tutorials, l[1]), l[1]);
        return [l[2], isFinite(firstTry) ? Math.round(firstTry * 10) / 10 : 0];
      }), { yTitle: '%' }),
    chart('Where players failed (lesson: object)', 'bar', ['Failure point', 'Failures'],
      tutorialLogCounts(tutorials, 'fail_log').slice(0, 15), { yTitle: 'Failures' }),
    chart('How players got past each object', 'bar', ['Object', 'Times'],
      tutorialLogCounts(tutorials, 'pass_log').slice(0, 20), { yTitle: 'Times' }),
    chart('Where players left the tutorial', 'bar', ['Lesson', 'Attempts'], vizCount(quits, 'quit_step', lessonLabels), { yTitle: 'Attempts' })
  ]};

  // Engagement
  var days = {};
  runs.forEach(function (r) { var d = vizDay(vizTime(r.ts), tz); days[d] = (days[d] || 0) + 1; });
  var perPlayer = {};
  runs.forEach(function (r) { if (r.player_id) perPlayer[r.player_id] = (perPlayer[r.player_id] || 0) + 1; });
  var quitLabels = { title: 'Title screen', tutorial: 'During the tutorial', mid_run: 'Mid-run', game_over_screen: 'Game over screen',
    paused_run: 'Paused mid-run', paused_tutorial: 'Paused in the tutorial' };
  var engagementSection = { title: 'Engagement', subtitle: 'when and how much people play', charts: [
    chart('Runs per day', 'column', ['Day', 'Runs'], Object.keys(days).sort().map(function (d) { return [d.split('|')[1], days[d]]; })),
    chart('Runs per player', 'column', ['Runs played', 'Players'], vizBins(Object.keys(perPlayer).map(function (p) { return perPlayer[p]; }),
      [1, 2, 3, 6, 11], vizCountLabel), { yTitle: 'Players' }),
    chart('Runs by build', 'pie', ['Build', 'Runs'], vizCount(runs, 'build')),
    chart('Where sessions ended', 'bar', ['Place', 'Sessions'], vizCount(sessions, 'quit_at', quitLabels), { yTitle: 'Sessions' }),
    chart('Session length', 'column', ['Minutes', 'Sessions'], vizBins(vizNums(sessions, 'session_s').map(function (s) { return s / 60; }),
      [0, 1, 3, 5, 10, 20], function (lo, hi) { return hi === null ? lo + '+ min' : lo + '–' + hi + ' min'; }), { yTitle: 'Sessions' }),
    chart('Pauses and tab-aways per run (average)', 'bar', ['Interruption', 'Per run'], [
      ['Pauses', vizMean(runs, 'pauses')], ['Clicked or tabbed away', vizMean(runs, 'focus_losses')]
    ], { yTitle: 'Per run' })
  ]};

  return {
    kpis: kpis,
    counts: { runs: runs.length, deaths: deaths.length },
    sections: [survivalSection, deathSection, shootingSection, pickupSection, obstacleSection, progressSection,
      tutorialSection, engagementSection]
  };
}

// ---------------------------------------------------------------- WorldShift metrics (pure: no Sheets calls)

/** Charts for the WorldShift escape: one run crosses legs (City, Desert, ...) and ends banked, lost or abandoned. */
function wsVizCompute(data, opts, tz) {
  function keep(r) {
    var t = vizTime(r.ts);
    if (t === null || (opts.from && t < opts.from) || (opts.until && t > opts.until)) return false;
    var build = String(r.build || ''), editor = /editor|test|ignore/i.test(build);
    if (opts.builds === VIS_BUILDS[1]) return !!build && !editor;
    if (opts.builds === VIS_BUILDS[2]) return editor;
    return true;
  }
  // A tab-close beacon can overlap the normal upload; keep the real ending.
  function latest(rows, key) {
    var byKey = {}, order = [];
    rows.filter(keep).forEach(function (r) {
      var k = key(r), previous = byKey[k];
      if (!previous) order.push(k);
      if (!previous || previous.outcome === 'tab_closed' || previous.status === 'tab_closed') byKey[k] = r;
    });
    return order.map(function (k) { return byKey[k]; }).sort(function (a, b) { return vizTime(a.ts) - vizTime(b.ts); });
  }
  function chart(title, type, header, rows, extra) {
    var spec = { title: title, type: type, header: header, rows: rows };
    Object.keys(extra || {}).forEach(function (k) { spec[k] = extra[k]; });
    return spec;
  }
  function median0(xs) { return median(xs) === '' ? 0 : median(xs); }
  function after(r) { return truthy(r.death_after_switch); }
  var runs = latest(data.ws_runs || [], function (r) { return r.run_id; });
  var legs = latest(data.ws_legs || [], function (r) { return r.run_id + '#' + r.leg; });
  var tutorials = latest(data.ws_tutorials || [], function (r) { return r.session_id + '#' + r.attempt; });
  var lost = filter(runs, 'outcome', 'lost'), banked = filter(runs, 'outcome', 'banked');
  var deadLegs = filter(legs, 'outcome', 'lost');
  var gates = legs.filter(function (l) { return l.outcome === 'exit' || l.outcome === 'next'; });
  var forked = legs.filter(function (l) { return l.fork === 'left' || l.fork === 'right'; });
  var envs = ['City', 'Desert'];
  var players = {};
  runs.forEach(function (r) { if (r.player_id) players[r.player_id] = true; });

  var kpis = [
    ['Escape runs', String(runs.length)],
    ['Players (browsers)', String(Object.keys(players).length)],
    ['Runs cashed out', vizPct(banked.length, runs.length) + '%'],
    ['Median legs cleared', String(median0(vizNums(runs, 'legs_cleared')))],
    ['Median driving time', median0(vizNums(runs, 'drive_s')) + ' s'],
    ['Best haul banked', '$' + vizMax(vizNums(banked, 'haul'))],
    ['Deaths right after a switch', vizPct(lost.filter(after).length, lost.length) + '%'],
    ['Switches per minute', String(median0(vizNums(runs, 'switches_per_min')))],
    ['Time in Night', median0(vizNums(runs, 'night_pct')) + '%'],
    ['Took the risky fork', vizPct(filter(forked, 'fork', 'right').length, forked.length) + '%'],
    ['Took Next at the gate', vizPct(filter(gates, 'outcome', 'next').length, gates.length) + '%'],
    ['Tutorials completed', filter(tutorials, 'status', 'completed').length + ' of ' + tutorials.length]
  ];

  var outcomeLabels = { banked: 'Cashed out', lost: 'Lost the haul', menu: 'Left to the menu', restart: 'Restarted',
    tab_closed: 'Closed the tab' };
  var progress = { title: 'Progress & outcomes', subtitle: 'how far escapes get', charts: [
    chart('How escape runs ended', 'pie', ['Ending', 'Runs'], vizCount(runs, 'outcome', outcomeLabels),
      { colors: ['#C2553A', '#4C947B', '#8A8F98', '#D8A35A', '#7B6BA8'] }),
    chart('Legs cleared per run', 'column', ['Legs cleared', 'Runs'], vizBins(vizNums(runs, 'legs_cleared'), [0, 1, 2, 3, 4], vizCountLabel),
      { yTitle: 'Runs' }),
    chart('City vs Desert: what happened on each visit', 'stacked', ['Environment', 'Reached the gate', 'Died', 'Left / restarted'],
      envs.map(function (env) {
        var here = filter(legs, 'env', env), cleared = here.filter(function (l) { return l.outcome === 'exit' || l.outcome === 'next'; });
        var died = filter(here, 'outcome', 'lost').length;
        return [env, cleared.length, died, here.length - cleared.length - died];
      }), { colors: ['#4C947B', '#C2553A', '#C9D1D9'], yTitle: 'Visits' }),
    chart('Driving time per run', 'column', ['Driving time', 'Runs'],
      vizBins(vizNums(runs, 'drive_s'), [0, 30, 60, 90, 120, 180, 240], vizSecondsLabel), { yTitle: 'Runs' })
  ]};

  var failureLabels = { 'CAUGHT': 'Caught by police', 'OUT OF FUEL': 'Out of fuel', 'MISSED LANDING': 'Missed a landing',
    'FELL OFF ROAD': 'Fell off the road' };
  var bins = [0, 250, 500, 750, 1000, 1500, 2000, 2500, 3000];
  var deaths = { title: 'Where & how players die', subtitle: 'per environment', charts: [
    chart('Why runs were lost', 'pie', ['Cause', 'Runs'], vizCount(lost, 'failure', failureLabels)),
    chart('Deaths by distance into the level', 'column', ['Distance (m)'].concat(envs),
      bins.map(function (lo, i) {
        var hi = i + 1 < bins.length ? bins[i + 1] : null;
        return [hi === null ? lo + ' m+' : lo + '–' + hi + ' m'].concat(envs.map(function (env) {
          return vizNums(filter(deadLegs, 'env', env), 'death_dist_m').filter(function (d) { return d >= lo && (hi === null || d < hi); }).length;
        }));
      }), { yTitle: 'Deaths' }),
    chart('Last thing hit before dying', 'bar', ['Obstacle', 'Deaths'],
      vizCount(deadLegs.filter(function (l) { return l.death_last_impact; }), 'death_last_impact'), { yTitle: 'Deaths' }),
    chart('Deaths per visit, by environment (%)', 'column', ['Environment', '% of visits ending in death'],
      envs.map(function (env) { var here = filter(legs, 'env', env); return [env, vizPct(filter(here, 'outcome', 'lost').length, here.length)]; }))
  ]};

  var sinceSwitch = vizNums(lost, 'death_s_since_switch');
  var impacts = vizSum(runs, 'impacts'), impactsAfter = vizSum(runs, 'impacts_after_switch');
  var switching = { title: 'World switching', subtitle: 'does Day / Night create decisions, and is it fair?', charts: [
    chart('Deaths within 1.5 s of a switch', 'pie', ['Death', 'Runs'],
      lost.length ? [['Right after a switch', lost.filter(after).length], ['Not after a switch', lost.length - lost.filter(after).length]] : [],
      { colors: ['#C2553A', '#C9D1D9'] }),
    chart('Seconds between the last switch and death', 'column', ['Seconds', 'Deaths'],
      vizBins(sinceSwitch, [0, 0.5, 1, 1.5, 3, 6, 12], vizSecondsLabel), { yTitle: 'Deaths' }),
    chart('Impacts within 1.5 s of a switch', 'pie', ['Impact', 'Impacts'],
      impacts ? [['Right after a switch', impactsAfter], ['Other impacts', impacts - impactsAfter]] : [], { colors: ['#C2553A', '#C9D1D9'] }),
    chart('Switches per minute, per run', 'column', ['Switches / min', 'Runs'],
      vizBins(vizNums(runs, 'switches_per_min'), [0, 2, 4, 6, 10, 15], function (lo, hi) { return hi === null ? lo + '+' : lo + '–' + hi; }),
      { yTitle: 'Runs' }),
    chart('Share of driving time in Night, per run', 'column', ['% in Night', 'Runs'],
      vizBins(vizNums(runs, 'night_pct'), [0, 10, 25, 40, 60, 80], function (lo, hi) { return hi === null ? lo + '%+' : lo + '–' + hi + '%'; }),
      { yTitle: 'Runs' }),
    chart('Switching vs driving time', 'scatter', ['Switches per minute', 'Driving time (s)'],
      runs.map(function (r) { return [Number(r.switches_per_min) || 0, Number(r.drive_s) || 0]; }), { trend: true })
  ]};

  var shots = vizSum(runs, 'shots'), hits = vizSum(runs, 'shot_hits');
  var choices = { title: 'Choices & rewards', subtitle: 'forks, Exit vs Next, power-ups', charts: [
    chart('Fork choice by environment', 'stacked', ['Environment', 'Left (safe)', 'Right (risky loot)'],
      envs.map(function (env) { var here = filter(forked, 'env', env); return [env, filter(here, 'fork', 'left').length, filter(here, 'fork', 'right').length]; }),
      { colors: ['#4C947B', '#D8A35A'], yTitle: 'Visits' }),
    chart('Loot boxes per visit, by fork taken', 'column', ['Fork', 'Mean loot boxes'],
      ['left', 'right'].map(function (f) { return [f === 'left' ? 'Left (safe)' : 'Right (risky)', vizMean(filter(forked, 'fork', f), 'loot_boxes')]; })),
    chart('Gate choice by leg', 'stacked', ['Leg', 'Exit (cash out)', 'Next (keep going)'],
      unique(gates, 'leg').sort(function (a, b) { return a - b; }).map(function (leg) {
        var here = filter(gates, 'leg', leg);
        return ['Leg ' + (Number(leg) + 1), filter(here, 'outcome', 'exit').length, filter(here, 'outcome', 'next').length];
      }), { colors: ['#4C947B', '#D8A35A'], yTitle: 'Gates' }),
    chart('Power-up picked per visit', 'pie', ['Power-up', 'Visits'], vizCount(legs, 'powerup', { '(blank)': 'None' })),
    chart('Shooting accuracy', 'pie', ['Shot', 'Shots'], shots ? [['Hit', hits], ['Missed', shots - hits]] : [], { colors: ['#4C947B', '#C9D1D9'] })
  ]};

  var steps = {};
  tutorials.forEach(function (t) { Object.keys(t).forEach(function (k) { if (/^s_\d+_/.test(k)) steps[k.substring(2)] = true; }); });
  var stepKeys = Object.keys(steps).sort();
  function stepLabel(step) { return step.replace(/^\d+_/, '').replace(/_/g, ' '); }
  var tutorial = { title: 'Tutorial', subtitle: 'completion, time and retries per lesson', charts: [
    chart('Tutorial outcomes', 'pie', ['Outcome', 'Attempts'], vizCount(tutorials, 'status'), { colors: ['#4C947B', '#D8A35A', '#C2553A', '#8A8F98'] }),
    chart('Average seconds per lesson', 'bar', ['Lesson', 'Seconds'],
      stepKeys.map(function (s) { return [stepLabel(s), vizMean(tutorials.filter(function (t) { return Number(t['s_' + s]) > 0; }), 's_' + s)]; }),
      { yTitle: 'Seconds' }),
    chart('Average retries per lesson', 'bar', ['Lesson', 'Retries'],
      stepKeys.map(function (s) { return [stepLabel(s), vizMean(tutorials.filter(function (t) { return Number(t['s_' + s]) > 0; }), 'retries_' + s)]; }),
      { yTitle: 'Retries' }),
    chart('Where players left the tutorial', 'bar', ['Lesson', 'Attempts'],
      vizCount(tutorials.filter(function (t) { return t.status !== 'completed'; }), 'step_title'), { yTitle: 'Attempts' })
  ]};

  var days = {}, perPlayer = {};
  runs.forEach(function (r) {
    var d = vizDay(vizTime(r.ts), tz); days[d] = (days[d] || 0) + 1;
    if (r.player_id) perPlayer[r.player_id] = (perPlayer[r.player_id] || 0) + 1;
  });
  var learningGroups = {};
  runs.forEach(function (r) {
    var n = Number(r.run_number_session);
    if (!n) return;
    var g = n >= 5 ? 'Run 5+' : 'Run ' + n;
    (learningGroups[g] = learningGroups[g] || []).push(r);
  });
  var engagement = { title: 'Engagement', subtitle: 'how much people play', charts: [
    chart('Runs per day', 'column', ['Day', 'Runs'], Object.keys(days).sort().map(function (d) { return [d.split('|')[1], days[d]]; })),
    chart('Runs per player', 'column', ['Runs played', 'Players'], vizBins(Object.keys(perPlayer).map(function (p) { return perPlayer[p]; }),
      [1, 2, 3, 6, 11], vizCountLabel), { yTitle: 'Players' }),
    chart('Learning curve: mean driving time by run # in a session', 'column', ['Run in session', 'Mean driving time (s)'],
      Object.keys(learningGroups).sort().map(function (g) { return [g + ' (' + learningGroups[g].length + ')', vizMean(learningGroups[g], 'drive_s')]; }))
  ]};

  return {
    kpis: kpis,
    counts: { runs: runs.length, deaths: lost.length, label: runs.length + ' escape runs (' + lost.length + ' lost) and ' + tutorials.length + ' tutorial attempts' },
    sections: [progress, deaths, switching, choices, tutorial, engagement]
  };
}
