"""Render level previews the way LevelManager lays them out (turn 0).
Usage: python3 Tools/render.py <project_root>   -> Previews/seviye_N.png + tum_seviyeler.png
"""
import sys, os
import numpy as np
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(__file__))
import solver

ROOT = sys.argv[1]
ART = os.path.join(ROOT, 'Assets/Resources/KayipParca/Art')
LV = os.path.join(ROOT, 'Assets/Resources/KayipParca/Levels')
OUT = os.path.join(ROOT, 'Previews')
T = 32
KEYS = ['T0_Kutu', 'T1_Hali', 'T2_Mutfak', 'T3_Oyuncak', 'T4_Gece', 'T5_Kedi', 'T6_Masa', 'T7_Resim']
PATH_COL = (209, 75, 217, 255); FIRE_COL = (255, 150, 40, 255); SLOT_COL = (255, 210, 63, 255)
_cache = {}


def L(p):
    if p not in _cache: _cache[p] = Image.open(os.path.join(ART, p)).convert('RGBA')
    return _cache[p]


def tint(im, c):
    a = np.array(im).astype(np.float32); a[..., :3] *= np.array(c[:3]) / 255.; a[..., 3] *= c[3] / 255.
    return Image.fromarray(a.clip(0, 255).astype(np.uint8))


def render(path, idx):
    lv = solver.Level(path)
    th = int(lv.meta['THEME']); key = KEYS[th]
    W, H = lv.W, lv.H
    pad = 2
    cw, ch = (W + pad * 2) * T, (H + pad * 2) * T
    img = Image.new('RGBA', (cw, ch), (12, 12, 16, 255))
    try:
        bg = L(f'Themes/{key}/bg_far.png'); s = max(cw / bg.width, ch / bg.height)
        img.alpha_composite(bg.resize((int(bg.width * s) + 1, int(bg.height * s) + 1), Image.NEAREST), (0, 0))
        nb = L(f'Themes/{key}/bg_near.png')
        img.alpha_composite(nb.resize((int(nb.width * s) + 1, int(nb.height * s) + 1), Image.NEAREST), (0, 0))
    except FileNotFoundError: pass
    ox, oy = pad * T, pad * T
    put = lambda im, c: img.alpha_composite(im, (ox + c[0] * T + (T - im.width) // 2, oy + c[1] * T + (T - im.height) // 2))
    wall = lambda x, y: lv.at((x, y)) == '#'
    inside = lambda x, y: 0 <= x < W and 0 <= y < H
    for y in range(H):
        for x in range(W):
            c = lv.at((x, y))
            if c == '#':
                bits = ''.join('1' if inside(nx, ny) and not wall(nx, ny) else '0' for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)))
                put(L(f'Themes/{key}/wall_{bits}.png'), (x, y)); continue
            v = (x * 7 + y * 13) % 9
            fl = 'floor_alt' if c == ',' else ('floor' if v < 7 else f'floor_{v - 6}')
            put(L(f'Themes/{key}/{fl}.png'), (x, y))
            t_, l_ = wall(x, y - 1), wall(x - 1, y)
            if t_ or l_: put(L(f"Common/shadow_{'tl' if t_ and l_ else 't' if t_ else 'l'}.png"), (x, y))
            if c == 'o': put(L(f'Themes/{key}/obstacle.png'), (x, y))
            if c == 'X': put(L(f'Themes/{key}/hazard.png'), (x, y))
            if c == 'S': put(L('Common/station.png'), (x, y))
            if c == 'K': put(L('Common/console.png'), (x, y))
            if c == 'T': put(L('Common/trash.png'), (x, y))
            if c == 'B': put(L('Common/pirate_box.png'), (x, y))
            if c in lv.goals and c in 'GEg':
                col = SLOT_COL if c != 'g' else (143, 211, 255, 255)
                put(tint(L(f'Slots/slot_{lv.goals[c]}.png'), col), (x, y))
    # enemy paths (style B)
    for e in lv.enemies:
        cells = [solver.enemy_pos(e, k) for k in range(abs(e[2] - e[0]) + abs(e[3] - e[1]) + 1)]
        for c in cells[1:-1]: put(tint(L('Common/path_dot.png'), PATH_COL), c)
        put(tint(L('Common/path_end.png'), PATH_COL), cells[-1])
        put(L('Common/next_frame.png'), solver.enemy_pos(e, 1))
    # lasers at t=0
    for l in lv.lasers:
        put(L(f"Common/emitter_{'NESW'[l['d']]}.png"), l['e'])
        o = 'v' if l['d'] in (0, 2) else 'h'
        on0, on1 = solver.laser_on(l, 0), solver.laser_on(l, 1)
        for c in l['cells']:
            if on0: put(L(f'Common/laser_{o}.png'), c)
            elif on1: put(L(f'Common/laser_{o}_warn.png'), c)
    # fire lanes
    for f in lv.fires:
        for c in f['cells'][1:]: put(tint(L('Common/path_dot.png'), FIRE_COL), c)
        for c in f['cells']:
            if solver.fire_at(f, 0, c):
                fb = L('Common/fireball_0.png')
                if f['d'] == 1: fb = fb.transpose(Image.FLIP_LEFT_RIGHT)
                put(fb, c)
        if solver.fire_at(f, 1, f['cells'][0]): put(L('Common/fire_warn.png'), f['cells'][0])
    # dragon
    ds = [(x, y) for y in range(H) for x in range(W) if lv.at((x, y)) in 'DY']
    if ds:
        x0, y0 = min(p[0] for p in ds), min(p[1] for p in ds)
        img.alpha_composite(L('Common/dragon_awake.png'), (ox + x0 * T, oy + y0 * T))
        yc = lv.find['Y']
        put(L('Common/yamali.png') if solver.chest_open(lv, 0) else L('Common/chest_closed.png'), yc)
        if lv.fires:
            f = lv.fires[0]; k = 1
            while k < 9 and not solver.fire_at(f, k, f['cells'][0]): k += 1
            put(L(f'Common/num_{k}.png'), (x0, max(0, y0 - 1)))
    # enemies + player + clone
    spr = 'Enemies/paw_0.png' if th == 5 else 'Enemies/ugly_0.png'
    for e in lv.enemies: put(L(spr), (e[0], e[1]))
    p = lv.find['P']
    for lay in (f"Player/body_{lv.meta['SHAPE']}.png", 'Player/face_idle_0.png', 'Player/hat.png'): put(L(lay), p)
    if 'C' in lv.find:
        put(tint(L(f"Player/body_{solver.mirror(lv.meta['SHAPE'])}.png"), (143, 211, 255, 220)), lv.find['C']); put(L('Player/face_idle_0.png'), lv.find['C'])
    # crane
    if 'H' in lv.find:
        for x in range(1, W - 1): put(L('Common/rail.png'), (x, 0))
        h = lv.find['H']; put(L('Common/trolley.png'), (h[0], 0))
        cab = L('Common/cable.png').resize((4, max(1, (h[1]) * T - 8)))
        img.alpha_composite(cab, (ox + h[0] * T + 14, oy + 24))
        put(L('Common/hook.png'), h)
    if th == 4:  # darkness preview
        a = np.array(img).astype(np.float32)
        cx, cy = ox + p[0] * T + 16, oy + p[1] * T + 16
        yy, xx = np.mgrid[0:img.height, 0:img.width]
        lf = np.clip(1.15 - np.hypot(xx - cx, yy - cy) / (T * 4.5), 0.25, 1.0)
        a[..., :3] *= lf[..., None]; img = Image.fromarray(a.clip(0, 255).astype(np.uint8))
    big = img.resize((img.width * 2, img.height * 2), Image.NEAREST)
    out = Image.new('RGBA', (big.width, big.height + 64), (20, 20, 20, 255))
    out.alpha_composite(big, (0, 64))
    d = ImageDraw.Draw(out)
    d.text((20, 14), f"{idx}. {lv.meta['NAME']}", font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', 30), fill=(249, 199, 79, 255))
    return out


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    files = [l.strip() for l in open(os.path.join(LV, 'bolumler.txt'), encoding='utf-8') if l.strip() and not l.startswith('#') and '=' not in l]
    rs = []
    for i, f in enumerate(files, 1):
        r = render(os.path.join(LV, f + '.txt'), i); r.save(os.path.join(OUT, f'seviye_{i}.png')); rs.append(r)
    cw = 900
    th_ = [r.resize((cw, int(r.height * cw / r.width)), Image.LANCZOS) for r in rs]
    rows = [max(th_[k].height, th_[k + 1].height) for k in range(0, len(th_), 2)]
    sheet = Image.new('RGB', (cw * 2 + 30, sum(rows) + 10 * (len(rows) + 1)), (11, 13, 15))
    y = 10
    for r in range(len(rows)):
        for c in range(2):
            if r * 2 + c < len(th_): sheet.paste(th_[r * 2 + c], (10 + c * (cw + 10), y))
        y += rows[r] + 10
    sheet.save(os.path.join(OUT, 'tum_seviyeler.png'))
    for old in ('seviye_7.png', 'seviye_8.png'): pass
    print('ok', len(rs))
