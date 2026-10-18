"""Generate four GDD figures from the exported workbook; leaves the workbook unchanged."""
import re
import argparse
import math
import statistics
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.ticker import MaxNLocator

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('workbook', type=Path, help='Google Sheets export (.xlsx) containing a runs tab')
parser.add_argument('--output', type=Path, required=True, help='Folder for generated PNG charts')
args = parser.parse_args()
SOURCE, OUT = args.workbook, args.output
OUT.mkdir(parents=True, exist_ok=True)
NS = {'s': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
with zipfile.ZipFile(SOURCE) as z:
    shared = []
    if 'xl/sharedStrings.xml' in z.namelist():
        shared = [''.join(e.itertext()) for e in ET.fromstring(z.read('xl/sharedStrings.xml')).findall('s:si', NS)]
    wb = ET.fromstring(z.read('xl/workbook.xml'))
    rels = {e.attrib['Id']: e.attrib['Target'] for e in ET.fromstring(z.read('xl/_rels/workbook.xml.rels'))}
    sheet = next(e for e in wb.findall('s:sheets/s:sheet', NS) if e.attrib['name'] == 'runs')
    target = rels[sheet.attrib['{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id']]
    target = target.lstrip('/') if target.startswith('/') else 'xl/' + target
    raw = []
    for row in ET.fromstring(z.read(target)).findall('s:sheetData/s:row', NS):
        values = {}
        for c in row.findall('s:c', NS):
            v = c.find('s:v', NS)
            value = v.text if v is not None else ''
            if c.attrib.get('t') == 's': value = shared[int(value)]
            elif c.attrib.get('t') == 'inlineStr': value = ''.join(c.find('s:is', NS).itertext())
            values[re.sub(r'\d+', '', c.attrib['r'])] = value
        raw.append(values)
    headers = raw.pop(0)
    records = [{headers[k]: v for k, v in row.items() if k in headers} for row in raw]

by_id = {}
for r in records:
    if not r.get('build') or re.search('editor|test|ignore', r['build'], re.I): continue
    key = r['run_id']
    if key not in by_id or by_id[key]['end_reason'] == 'quit_tab_closed': by_id[key] = r
runs = [r for r in by_id.values() if r.get('end_reason') in ('fuel', 'police')]
if not runs:
    parser.error('No non-test runs ending in fuel depletion or police capture were found.')
def total(key): return sum(int(float(r.get(key, 0) or 0)) for r in runs)
times = [float(r['survival_s']) for r in runs]
n = len(runs)
plt.rcParams.update({'font.size': 12, 'axes.spines.top': False, 'axes.spines.right': False, 'axes.titleweight': 'bold'})
dates = sorted({r.get('ts', '')[:10] for r in runs if r.get('ts')})
period = ' to '.join(dict.fromkeys([dates[0], dates[-1]])) if dates else 'Date unavailable'
footer = f'Actual recorded data • {period}\n{n} runs ended by fuel or police; editor tests, quits and restarts excluded.'
def finish(fig, filename):
    fig.text(.09, .035, footer, fontsize=9, color='#555555', linespacing=1.5)
    fig.savefig(OUT / filename, dpi=200, facecolor='white')
    plt.close(fig)
def grid(ax):
    ax.set_axisbelow(True)
    ax.grid(axis='y', alpha=.16)
    ax.yaxis.set_major_locator(MaxNLocator(integer=True))

fig, ax = plt.subplots(figsize=(9, 5.8))
fig.subplots_adjust(left=.10, right=.96, top=.81, bottom=.23)
bin_width = max(30, math.ceil(max(times) / 12 / 30) * 30)
upper = max(bin_width, math.ceil(max(times) / bin_width) * bin_width)
values, edges, bars = ax.hist(times, bins=list(range(0, upper + 1, bin_width)), color='#487DAD', edgecolor='white')
for value, bar in zip(values, bars):
    if value: ax.text(bar.get_x()+bar.get_width()/2, value+.15, str(int(value)), ha='center', fontsize=10)
ax.axvline(statistics.median(times), color='#C26033', linestyle='--', linewidth=2, label=f'Median: {statistics.median(times):.1f} seconds')
ax.set(xlabel='Survival time (seconds)', ylabel='Number of runs', xlim=(0,upper), ylim=(0,max(values)+3))
ax.set_xticks(range(0,upper+1,bin_width*2)); ax.legend(frameon=False); grid(ax)
fig.suptitle('Player Survival Time', x=.10, ha='left', fontsize=20, fontweight='bold')
fig.text(.10,.86,f'{sum(t<30 for t in times)} of {n} runs ended before 30 seconds.',fontsize=12)
finish(fig,'09-metric-player-survival-time.png')

fig, ax = plt.subplots(figsize=(9, 5.8))
fig.subplots_adjust(left=.23, right=.94, top=.78, bottom=.23)
counts = [sum(r['end_reason']==reason for r in runs) for reason in ('fuel','police')]
bars=ax.barh(['Out of fuel','Caught by police'],counts,color=['#D59A39','#516C9D'],height=.48)
for b,v in zip(bars,counts): ax.text(v+.35,b.get_y()+b.get_height()/2,f'{v} ({v/n:.1%})',va='center')
ax.invert_yaxis(); ax.set(xlabel='Number of deaths',xlim=(0,max(counts)*1.35))
ax.xaxis.set_major_locator(MaxNLocator(integer=True)); ax.set_axisbelow(True); ax.grid(axis='x',alpha=.16)
fig.suptitle('Player Deaths',x=.10,ha='left',fontsize=20,fontweight='bold')
fig.text(.10,.85,'Which condition ended the run?',fontsize=12)
finish(fig,'10-metric-player-deaths.png')

fig, ax = plt.subplots(figsize=(9,5.8))
fig.subplots_adjust(left=.05,right=.65,top=.83,bottom=.18)
shots,hits=total('shots_fired'),total('shots_hit')
if hits > shots:
    parser.error('Successful hits exceed shots fired; check the exported data.')
ax.pie([hits,shots-hits] if shots else [0,1],colors=['#398774','#D5DBE0'],startangle=90,counterclock=False,wedgeprops={'width':.27,'edgecolor':'white'})
ax.text(0,.07,f'{hits/shots:.1%}' if shots else 'No shots',ha='center',fontsize=30,fontweight='bold',color='#286B5B')
ax.text(0,-.20,'overall accuracy',ha='center',fontsize=12)
fig.suptitle('Shooting Accuracy',x=.10,ha='left',fontsize=20,fontweight='bold')
fig.text(.65,.62,f'{hits} successful hits',fontsize=15,color='#286B5B')
fig.text(.65,.52,f'{shots-hits} shots without a hit',fontsize=12)
fig.text(.65,.42,f'{shots} total shots',fontsize=12)
fig.text(.65,.29,'A hit does not always\ndestroy an obstacle.',fontsize=10,color='#555555')
finish(fig,'11-metric-shooting-accuracy.png')

fig,ax=plt.subplots(figsize=(9,6.5))
fig.subplots_adjust(left=.10,right=.96,top=.81,bottom=.32)
collected=[total(k+'_collected') for k in ('fuel','ammo','mystery')]
missed=[total(k+'_missed') for k in ('fuel','ammo','mystery')]
bars1=ax.bar([i-.19 for i in range(3)],collected,width=.36,label='Collected',color='#4C947B')
bars2=ax.bar([i+.19 for i in range(3)],missed,width=.36,label='Missed',color='#D8A35A')
ax.bar_label(bars1,padding=4);ax.bar_label(bars2,padding=4)
ax.set_xticks(range(3),['Fuel','Ammunition','Mystery boxes'])
ax.set(ylabel='Number of pickups',ylim=(0,max(1,max(collected+missed))*1.25))
ax.legend(frameon=False);grid(ax)
fig.suptitle('Pickup Collection',x=.10,ha='left',fontsize=20,fontweight='bold')
fig.text(.10,.86,'Collected and missed pickups across completed survival attempts',fontsize=12)
fig.text(.10,.19,f'Ammo pickups touched while full: {total("ammo_wasted_full")}',fontsize=13,fontweight='bold')
fig.text(.10,.14,'This count can overlap missed ammo; do not add it to the bars.',fontsize=10,color='#555555')
finish(fig,'12-metric-pickup-collection.png')
print(f'Saved four charts to {OUT}')
print({'runs':n,'shots':shots,'hits':hits,'collected':collected,'missed':missed,'ammo_wasted_full':total('ammo_wasted_full')})
