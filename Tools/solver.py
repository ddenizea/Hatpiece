"""BFS solver that mirrors the rules in LevelManager.cs exactly.
Usage: python3 Tools/solver.py Assets/Resources/KayipParca/Levels [levelN.txt ...]

Rules (keep in sync with LevelManager.Step):
  actions: N E S W move, CW / CCW rotate, WAIT (Space). In crane mode: move hook, SPACE grab/drop/wait.
  - bumping into something solid costs no turn
  - after every turn: enemies step, lasers blink, fireballs fly
  - laser: on if off == 0 or (t + phase) % (on + off) < on; standing in an on beam kills
  - fireball: spawned at time s when (s + phase) % period == 0, at lane index k at time s + k
  - stepping onto 'K' (console) switches to crane mode; fire burning the hook sends it home
  - SPACE on 'Y' while the chest is open grabs Yamali -> dragon calms (fire stops, D/Y walkable)
  - SPACE on 'T' or 'B' while carrying drops it (ending) -> back to piece mode
  - win: shape matches the goal under you (G/E/Y; Y only after an ending was chosen)
"""
import sys, os, math
from collections import deque

DIRS = [(0, -1), (1, 0), (0, 1), (-1, 0)]
DIRN = {'N': 0, 'E': 1, 'S': 2, 'W': 3}
MIRROR = [0, 3, 2, 1]
BEAM_BLOCK = set('#oDTBY')


def rot_cw(s):  return s[3] + s[0] + s[1] + s[2]
def rot_ccw(s): return s[1] + s[2] + s[3] + s[0]
def mirror(s):  return s[0] + s[3] + s[2] + s[1]
def toggle(s, d): return s[:d] + ('0' if s[d] == '1' else '1') + s[d + 1:]


class Level:
    def __init__(self, path):
        self.meta, self.enemies, self.lasers, self.fires = {}, [], [], []
        rows, inmap = [], False
        for line in open(path, encoding='utf-8'):
            line = line.rstrip('\n').rstrip('\r')
            if inmap:
                if line: rows.append(line)
                continue
            if line == 'MAP': inmap = True; continue
            if '=' not in line or line.startswith('#'): continue
            k, v = line.split('=', 1)
            if k == 'ENEMY': self.enemies.append(tuple(map(int, v.split(','))))
            elif k == 'LASER':
                x, y, d, on, off, ph = v.split(',')
                self.lasers.append(dict(e=(int(x), int(y)), d=DIRN[d], on=int(on), off=int(off), ph=int(ph)))
            elif k == 'FIRE':
                x, y, d, per, ph = v.split(',')
                self.fires.append(dict(s=(int(x), int(y)), d=DIRN[d], per=int(per), ph=int(ph)))
            else: self.meta[k] = v
        self.W = max(len(r) for r in rows); self.H = len(rows)
        assert all(len(r) == self.W for r in rows), f'ragged rows: {[len(r) for r in rows]}'
        self.g = [list(r) for r in rows]
        self.find = {}
        for y, r in enumerate(self.g):
            for x, c in enumerate(r):
                if c in 'PCHKYTBL': self.find[c] = (x, y)
                if c in 'PCHL': self.g[y][x] = '.'
        self.goals = {k[5:]: v for k, v in self.meta.items() if k.startswith('GOAL_')}
        self.crane = 'K' in self.find
        ch = self.meta.get('CHEST', '1,4,0').split(',')
        self.chest = tuple(map(int, ch))
        for l in self.lasers: l['cells'] = self.ray(l['e'], l['d'], include_start=False)
        for f in self.fires: f['cells'] = self.ray(f['s'], f['d'], include_start=True)

    def at(self, p):
        x, y = p
        return self.g[y][x] if 0 <= x < self.W and 0 <= y < self.H else '#'

    def ray(self, start, d, include_start):
        cells, p = [], start
        if not include_start: p = (p[0] + DIRS[d][0], p[1] + DIRS[d][1])
        while self.at(p) not in BEAM_BLOCK:
            cells.append(p); p = (p[0] + DIRS[d][0], p[1] + DIRS[d][1])
        return cells


def enemy_pos(e, t):
    x1, y1, x2, y2 = e
    n = abs(x2 - x1) + abs(y2 - y1)
    if n == 0: return (x1, y1)
    k = t % (2 * n); s = k if k <= n else 2 * n - k
    dx = (x2 > x1) - (x2 < x1); dy = (y2 > y1) - (y2 < y1)
    return (x1 + dx * s, y1 + dy * s)


def laser_on(l, t):
    return l['off'] == 0 or (t + l['ph']) % (l['on'] + l['off']) < l['on']


def fire_at(f, t, c):
    if c not in f['cells']: return False
    s = t - f['cells'].index(c)
    return s >= 0 and (s + f['ph']) % f['per'] == 0


def fire_swap(f, t0, old, new):
    cells = f['cells']
    if new not in cells: return False
    k = cells.index(new)
    if k + 1 >= len(cells) or cells[k + 1] != old: return False
    s = t0 - k
    return s >= 0 and (s + f['ph']) % f['per'] == 0


def chest_open(L, t):
    o, cyc, ph = L.chest
    return (t + ph) % cyc < o


def period_of(L):
    p = 1
    for e in L.enemies:
        n = abs(e[2] - e[0]) + abs(e[3] - e[1])
        if n: p = p * (2 * n) // math.gcd(p, 2 * n)
    for l in L.lasers:
        if l['off']: p = p * (l['on'] + l['off']) // math.gcd(p, l['on'] + l['off'])
    for f in L.fires: p = p * f['per'] // math.gcd(p, f['per'])
    if L.crane: p = p * L.chest[1] // math.gcd(p, L.chest[1])
    return p


def solve(path, want_all_goals=True, rescue=False):
    L = Level(path)
    P = L.find['P']; Cs = L.find.get('C')
    shape = L.meta['SHAPE']
    per = period_of(L)
    pgoal = {k: v for k, v in L.goals.items() if k != 'g'}
    cgoal = L.goals.get('g')

    def solid_for_piece(c, calm):
        ch = L.at(c)
        if ch in '#oTB': return True
        if ch in 'DY' and not calm: return True
        return False

    def try_move(pos, shp, d, own_goals, calm):
        n = (pos[0] + DIRS[d][0], pos[1] + DIRS[d][1])
        if solid_for_piece(n, calm): return None
        ch = L.at(n)
        if ch in own_goals and own_goals[ch] != shp: return None
        if ch == 'S': shp = toggle(shp, d)
        return n, shp, ch == 'X'

    def hook_can(h):
        x, y = h
        if not (1 <= x < L.W - 1 and 1 <= y < L.H - 1): return False
        return L.at(h) != 'D'

    LP = L.find.get('L')
    # state: (p, ps, c, cs, t%per, mode, hook, carrying, calm, ending, follower_pos, following)
    start = (P, shape, Cs, mirror(shape) if Cs else None, 0, 0, L.find.get('H'), False, False, '', LP, False)
    seen = {start}; q = deque([(start, 0, [])]); results = {}
    names = ['N', 'E', 'S', 'W', 'CW', 'CCW', 'WAIT']
    while q:
        st, dist, hist = q.popleft()
        p, ps, c, cs, t, mode, hook, carry, calm, ending, fp, fol = st
        for a in range(7):
            np_, nps, nc, ncs, nmode, nhook, ncarry, ncalm, nend = p, ps, c, cs, mode, hook, carry, calm, ending
            nfp, nfol = fp, fol
            dead = False
            moved = False
            if mode == 0:
                if a < 4:
                    r = try_move(p, ps, a, pgoal, calm)
                    if r is None: continue
                    np_, nps, d1 = r; dead |= d1; moved = True
                    if c:
                        rc = try_move(c, cs, MIRROR[a], {'g': cgoal} if cgoal else {}, calm)
                        if rc: nc, ncs, d2 = rc; dead |= d2
                elif a in (4, 5):
                    nps = rot_cw(ps) if a == 4 else rot_ccw(ps)
                    if c: ncs = rot_ccw(cs) if a == 4 else rot_cw(cs)
            else:
                if a < 4:
                    h = (hook[0] + DIRS[a][0], hook[1] + DIRS[a][1])
                    if not hook_can(h): continue
                    nhook = h
                elif a == 6:
                    if not carry and hook == L.find['Y'] and chest_open(L, t): ncarry, ncalm = True, True
                    elif carry and hook in (L.find.get('T'), L.find.get('B')):
                        nend = 'T' if hook == L.find.get('T') else 'B'; ncarry = False; nmode = 0
                else: continue
            nt = t + 1
            # lost piece: trails one step behind; picked up by stepping on it
            if moved and fol: nfp = p
            if LP and not nfol and np_ == LP: nfol, nfp = True, LP
            if nfol:
                hit = False
                for e in L.enemies:
                    e0, e1 = enemy_pos(e, t), enemy_pos(e, nt)
                    if e1 == nfp or (e1 == fp and e0 == nfp and fol): hit = True
                for l in L.lasers:
                    if laser_on(l, nt) and nfp in l['cells']: hit = True
                if not ncalm:
                    for f in L.fires:
                        if fire_at(f, nt, nfp) or (fol and fire_swap(f, t, fp, nfp)): hit = True
                if hit: nfol, nfp = False, LP
            for e in L.enemies:
                e0, e1 = enemy_pos(e, t), enemy_pos(e, nt)
                for old, new in ((p, np_), (c, nc)):
                    if new is None: continue
                    if e1 == new or (e1 == old and e0 == new): dead = True
            for l in L.lasers:
                if laser_on(l, nt) and (np_ in l['cells'] or (nc and nc in l['cells'])): dead = True
            if not ncalm:
                for f in L.fires:
                    if fire_at(f, nt, np_) or fire_swap(f, t, p, np_): dead = True
                    if nmode == 1 and not ncarry and (fire_at(f, nt, nhook) or fire_swap(f, t, hook, nhook)):
                        nhook = L.find['H']
            if dead: continue
            if nmode == 0 and L.crane and not ncalm and L.at(np_) == 'K':
                nmode = 1; nhook = L.find['H']
            pc = L.at(np_)
            win = False
            if pc in pgoal and pgoal[pc] == nps and (not c or (L.at(nc) == 'g' and ncs == cgoal)):
                win = pc != 'Y' or (ncalm and nend != '')
                if rescue and LP and not nfol: win = False
            if win:
                key = (pc, nend)
                if key not in results: results[key] = (dist + 1, hist + [names[a]])
                if not want_all_goals: return L, results
                continue
            ns = (np_, nps, nc, ncs, nt % per, nmode, nhook, ncarry, ncalm, nend, nfp, nfol)
            if ns in seen: continue
            seen.add(ns); q.append((ns, dist + 1, hist + [names[a]]))
    return L, results


if __name__ == '__main__':
    d = sys.argv[1]
    files = sys.argv[2:] or sorted(f for f in os.listdir(d) if f.startswith('level') and f.endswith('.txt'))
    for f in files:
        L, res = solve(os.path.join(d, f))
        print(f, L.meta.get('NAME'), 'limit', L.meta.get('MOVES'))
        if not res: print('   UNSOLVABLE')
        for g, (n, h) in sorted(res.items()):
            print(f'   goal {g}: {n} moves  {" ".join(h)}')
        if 'L' in L.find:
            _, rr = solve(os.path.join(d, f), rescue=True)
            if not rr: print('   RESCUE UNSOLVABLE')
            for g, (n, h) in sorted(rr.items()):
                print(f'   rescue {g}: {n} moves  {" ".join(h)}')
