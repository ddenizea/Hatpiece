"""Procedural flat pixel-art for Hatpiece (v2, 3/4 flat style). 32x32 tiles, PPU 32.
Usage: python3 Tools/art.py <project_root>
Writes into Assets/Resources/KayipParca/Art. Cat paws and the old backgrounds of themes 0-5 are kept.
"""
import sys, os, math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = sys.argv[1]
ART = os.path.join(ROOT, 'Assets/Resources/KayipParca/Art')
T = 32
FONT_B = '/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf'
FONT_R = '/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'


def hx(h, a=255):
    h = h.lstrip('#'); return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def new(w=T, h=T, fill=(0, 0, 0, 0)): return Image.new('RGBA', (w, h), fill)


def save(img, *parts):
    p = os.path.join(ART, *parts); os.makedirs(os.path.dirname(p), exist_ok=True); img.save(p)


def load(*parts): return Image.open(os.path.join(ART, *parts)).convert('RGBA')


def soft(img, fn):
    ov = new(*img.size); fn(ImageDraw.Draw(ov)); img.alpha_composite(ov)


# ------------------------------------------------------------------ masks
YY, XX = np.mgrid[0:T, 0:T] + 0.5
C = 16.0


def piece_mask(shape, body=11.0, r=3.0):
    m = (np.abs(XX - C) <= body) & (np.abs(YY - C) <= body)
    centers = [(C, C - body), (C + body, C), (C, C + body), (C - body, C)]
    outward = [(0, -1), (1, 0), (0, 1), (-1, 0)]
    for i, bit in enumerate(shape):
        cx, cy = centers[i]; dx, dy = outward[i]
        if bit == '1':
            m |= (XX - cx - dx * 2.2) ** 2 + (YY - cy - dy * 2.2) ** 2 <= r * r
            m |= (np.abs(XX - cx - dx * .5) <= (1.6 if dx == 0 else 1.2)) & (np.abs(YY - cy - dy * .5) <= (1.2 if dx == 0 else 1.6))
        else:
            m &= ~((XX - cx + dx * 1.6) ** 2 + (YY - cy + dy * 1.6) ** 2 <= r * r)
            m &= ~((np.abs(XX - cx + dx * .5) <= (1.6 if dx == 0 else 1.2)) & (np.abs(YY - cy + dy * .5) <= (1.2 if dx == 0 else 1.6)))
    return m


def dilate(m, n=1):
    for _ in range(n):
        p = np.pad(m, 1)
        m = p[1:-1, 1:-1] | p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
    return m


def edge(m):
    p = np.pad(m, 1)
    return m & ~(p[:-2, 1:-1] & p[2:, 1:-1] & p[1:-1, :-2] & p[1:-1, 2:])


def from_masks(size, layers):
    a = np.zeros((size[1], size[0], 4), np.uint8)
    for m, col in layers: a[m] = col
    return Image.fromarray(a, 'RGBA')


SHAPES = [format(i, '04b') for i in range(16)]
PIECE, INK, BLUSH, HAT = hx('f4e9d8'), hx('1b1b1b'), hx('e88a8a'), hx('d94a3a')


# ------------------------------------------------------------------ player, slots
def make_player():
    for s in SHAPES:
        m = piece_mask(s)
        save(from_masks((T, T), [(m, PIECE), (edge(m), INK)]), 'Player', f'body_{s}.png')
        # slot: dark hole, inner lip, white 2px rim (tinted in Unity), sparkles
        rim = dilate(m, 2) & ~m
        lip = m & ~np.roll(m, 3, axis=0)
        im = from_masks((T, T), [(rim, hx('ffffff')), (m, hx('15151f')), (lip, hx('2e2e44'))])
        d = ImageDraw.Draw(im)
        for sx, sy in [(2, 2), (29, 4), (3, 29), (29, 28)]:
            d.point([(sx, sy), (sx - 1, sy), (sx + 1, sy), (sx, sy - 1), (sx, sy + 1)], fill=hx('ffffff'))
        save(im, 'Slots', f'slot_{s}.png')

    def face(kind):
        im = new(); d = ImageDraw.Draw(im)
        if kind in ('open', 'half', 'closed'):
            top = {'open': 13, 'half': 14, 'closed': 15}[kind]
            d.rectangle([12, top, 13, 16], fill=INK); d.rectangle([18, top, 19, 16], fill=INK)
            if kind == 'open': d.point([(12, 13), (18, 13)], fill=PIECE)
        elif kind == 'happy':
            for x0 in (11, 17):
                d.point([(x0, 16), (x0 + 1, 15), (x0 + 2, 15), (x0 + 3, 16)], fill=INK)
        elif kind == 'dead':
            for x0 in (11, 17):
                d.line([(x0, 13), (x0 + 2, 15)], fill=INK); d.line([(x0, 15), (x0 + 2, 13)], fill=INK)
        d.rectangle([10, 18, 11, 18], fill=BLUSH); d.rectangle([20, 18, 21, 18], fill=BLUSH)
        if kind == 'happy': d.line([(14, 19), (17, 19)], fill=INK); d.point([(14, 18), (17, 18)], fill=INK)
        elif kind == 'dead': d.line([(14, 20), (17, 20)], fill=INK)
        else: d.line([(15, 19), (16, 19)], fill=INK)
        return im
    for i, k in enumerate(['open', 'open', 'half', 'closed']): save(face(k), 'Player', f'face_idle_{i}.png')
    save(face('happy'), 'Player', 'face_happy.png'); save(face('dead'), 'Player', 'face_dead.png')
    im = new(); d = ImageDraw.Draw(im)
    d.rectangle([3, 7, 15, 9], fill=INK); d.rectangle([4, 8, 14, 8], fill=HAT)
    d.rectangle([6, 0, 12, 7], fill=INK); d.rectangle([7, 1, 11, 6], fill=HAT)
    d.rectangle([7, 5, 11, 5], fill=INK)
    save(im, 'Player', 'hat.png')
    # shards for the shatter effect
    rng = random.Random(3)
    for i in range(6):
        im = new(); d = ImageDraw.Draw(im)
        pts = []
        n = rng.choice([3, 4, 4, 5])
        for k in range(n):
            a = 2 * math.pi * k / n + rng.uniform(-.4, .4)
            rr = rng.uniform(4, 7)
            pts.append((16 + math.cos(a) * rr, 16 + math.sin(a) * rr))
        d.polygon(pts, fill=PIECE, outline=INK)
        save(im, 'Player', f'shard_{i}.png')


# ------------------------------------------------------------------ enemies
UG, UG_LINE, UG_PATCH, EYE, PUPIL = hx('8a9a5b'), hx('2c3320'), hx('c9b458'), hx('fff6d6'), hx('d61f3a')


def make_ugly():
    for f in range(4):
        m = piece_mask('1101', body=9.5, r=3.6)
        m2 = m.copy()
        for yy in range(4, 12): m2[yy] = np.roll(m[yy], 1)
        for yy in range(20, 26): m2[yy] = np.roll(m[yy], -1)
        bob = [0, 1, 0, -1][f]
        m2 = np.roll(m2, bob, axis=0)
        patch = np.zeros_like(m2); patch[17 + bob:24 + bob, 6:14] = True; patch &= m2
        im = from_masks((T, T), [(m2, UG), (patch, UG_PATCH), (edge(m2), UG_LINE)])
        d = ImageDraw.Draw(im)
        for sx in (7, 10, 13): d.line([(sx - 1, 16 + bob), (sx + 1, 18 + bob)], fill=UG_LINE)
        if f == 2:
            d.line([(14, 13 + bob), (23, 13 + bob)], fill=UG_LINE, width=2)
        else:
            d.ellipse([14, 9 + bob, 23, 18 + bob], fill=EYE, outline=UG_LINE)
            d.rectangle([18, 12 + bob, 20, 15 + bob], fill=PUPIL)
        d.line([(8, 12 + bob), (11, 11 + bob)], fill=UG_LINE, width=2)
        d.line([(12, 22 + bob), (14, 20 + bob), (16, 22 + bob), (18, 20 + bob), (20, 22 + bob), (22, 20 + bob)], fill=UG_LINE)
        save(im, 'Enemies', f'ugly_{f}.png')


# ------------------------------------------------------------------ common sprites
def make_common():
    W_ = hx('ffffff')
    im = new(); d = ImageDraw.Draw(im); d.ellipse([11, 11, 20, 20], fill=W_, outline=hx('4a4a4a')); save(im, 'Common', 'path_dot.png')
    im = new(); d = ImageDraw.Draw(im); d.ellipse([8, 8, 23, 23], outline=W_, width=3); save(im, 'Common', 'path_end.png')
    im = new(); d = ImageDraw.Draw(im)
    R = hx('ff2e4c'); L = 9
    for (cx, cy, sx, sy) in [(1, 1, 1, 1), (30, 1, -1, 1), (1, 30, 1, -1), (30, 30, -1, -1)]:
        d.line([(cx, cy), (cx + sx * L, cy)], fill=R, width=3); d.line([(cx, cy), (cx, cy + sy * L)], fill=R, width=3)
    soft(im, lambda o: o.rectangle([4, 4, 27, 27], fill=(255, 46, 76, 60)))
    save(im, 'Common', 'next_frame.png')
    # station pad (fixed colours)
    im = new(); d = ImageDraw.Draw(im)
    d.ellipse([3, 7, 28, 28], fill=hx('2a4060'))
    d.ellipse([3, 5, 28, 26], fill=hx('3a5a80'), outline=hx('90e0ef'), width=2)
    d.arc([9, 9, 22, 22], 200, 340, fill=W_, width=2); d.arc([9, 9, 22, 22], 20, 160, fill=W_, width=2)
    d.polygon([(21, 10), (24, 15), (18, 14)], fill=W_); d.polygon([(10, 21), (7, 16), (13, 17)], fill=W_)
    save(im, 'Common', 'station.png')
    # floor contact shadows
    for name, t_, l_ in [('shadow_t', 1, 0), ('shadow_l', 0, 1), ('shadow_tl', 1, 1)]:
        im = new()
        def f(o, t_=t_, l_=l_):
            if t_: o.rectangle([0, 0, 31, 5], fill=(20, 14, 10, 90))
            if l_: o.rectangle([0, 6 if t_ else 0, 3, 31], fill=(20, 14, 10, 55))
        soft(im, f); save(im, 'Common', f'{name}.png')
    # lasers
    RED, CORE = hx('ff2e4c'), hx('ffe1e8')
    for o in ('h', 'v'):
        im = new(); d = ImageDraw.Draw(im)
        if o == 'h': d.rectangle([0, 12, 31, 19], fill=RED); d.rectangle([0, 15, 31, 16], fill=CORE)
        else: d.rectangle([12, 0, 19, 31], fill=RED); d.rectangle([15, 0, 16, 31], fill=CORE)
        save(im, 'Common', f'laser_{o}.png')
        im = new(); d = ImageDraw.Draw(im)
        for k in range(0, 32, 8):
            if o == 'h': d.line([(k, 15), (k + 3, 15)], fill=RED, width=2)
            else: d.line([(15, k), (15, k + 3)], fill=RED, width=2)
        save(im, 'Common', f'laser_{o}_warn.png')
    for di, name in enumerate('NESW'):
        im = new(); d = ImageDraw.Draw(im)
        d.rounded_rectangle([9, 9, 22, 22], radius=3, fill=hx('3a3f47'), outline=hx('111318'))
        d.rectangle([12, 12, 19, 19], fill=hx('5b636e'))
        lx, ly = [(16, 9), (22, 16), (16, 22), (9, 16)][di]
        d.ellipse([lx - 3, ly - 3, lx + 3, ly + 3], fill=RED, outline=hx('111318'))
        save(im, 'Common', f'emitter_{name}.png')
    # fireballs (travelling west; flip in Unity for other directions)
    for f in range(4):
        im = new(); d = ImageDraw.Draw(im)
        ph = f * math.pi / 2
        tail = [(14, 9), (31, 12 + int(2 * math.sin(ph))), (24, 16), (31, 20 + int(2 * math.cos(ph))), (14, 23)]
        d.polygon(tail, fill=hx('e8452c'))
        d.ellipse([5, 9, 19, 23], fill=hx('ffb02e'), outline=hx('e8452c'))
        d.ellipse([8, 12, 15, 19], fill=hx('fff3b0'))
        save(im, 'Common', f'fireball_{f}.png')
    im = new(); d = ImageDraw.Draw(im)
    d.ellipse([7, 7, 24, 24], outline=hx('ffb02e'), width=2); d.ellipse([13, 13, 18, 18], fill=hx('e8452c'))
    save(im, 'Common', 'fire_warn.png')
    # crane parts
    Y_, YD, ST = hx('f2b134'), hx('7a5a12'), hx('3a3a3a')
    im = new(); d = ImageDraw.Draw(im)
    d.rectangle([0, 6, 31, 9], fill=Y_, outline=YD); d.rectangle([0, 22, 31, 25], fill=Y_, outline=YD)
    for k in range(0, 32, 16): d.line([(k, 9), (k + 8, 22)], fill=YD, width=2); d.line([(k + 8, 22), (k + 16, 9)], fill=YD, width=2)
    save(im, 'Common', 'rail.png')
    im = new(); d = ImageDraw.Draw(im)
    d.rectangle([7, 8, 24, 23], fill=Y_, outline=YD); d.ellipse([8, 4, 14, 10], fill=ST); d.ellipse([17, 4, 23, 10], fill=ST)
    d.rectangle([14, 23, 17, 31], fill=ST)
    save(im, 'Common', 'trolley.png')
    im = new(4, 32, hx('3a3a3a')); save(im, 'Common', 'cable.png')
    im = new(); d = ImageDraw.Draw(im)
    d.rectangle([14, 0, 17, 10], fill=ST)
    d.rectangle([9, 9, 22, 14], fill=hx('9aa5b1'), outline=hx('3a434d'))
    d.arc([7, 10, 17, 26], 90, 270, fill=hx('9aa5b1'), width=3); d.arc([14, 10, 24, 26], 270, 90, fill=hx('9aa5b1'), width=3)
    save(im, 'Common', 'hook.png')
    im = new(); d = ImageDraw.Draw(im)
    soft(im, lambda o: o.ellipse([4, 24, 29, 31], fill=(20, 14, 10, 90)))
    d.rectangle([5, 10, 26, 20], fill=Y_, outline=YD); d.rectangle([5, 20, 26, 28], fill=hx('c98d1e'), outline=YD)
    d.line([(12, 14), (12, 5)], fill=ST, width=2); d.ellipse([9, 2, 15, 8], fill=hx('f94144'), outline=hx('4a0f12'))
    for bx, col in ((18, 'f94144'), (22, '43aa8b')): d.ellipse([bx - 2, 13, bx + 2, 17], fill=hx(col))
    save(im, 'Common', 'console.png')
    im = new(); d = ImageDraw.Draw(im)
    soft(im, lambda o: o.ellipse([5, 25, 28, 31], fill=(20, 14, 10, 90)))
    d.rectangle([7, 9, 24, 28], fill=hx('8d99a6'), outline=hx('2f363d'))
    for x in (11, 15, 19): d.line([(x, 12), (x, 26)], fill=hx('6b7682'))
    d.rectangle([5, 5, 26, 9], fill=hx('aab4bf'), outline=hx('2f363d')); d.rectangle([13, 3, 18, 5], fill=hx('2f363d'))
    save(im, 'Common', 'trash.png')
    im = new(); d = ImageDraw.Draw(im)
    soft(im, lambda o: o.ellipse([3, 25, 29, 31], fill=(20, 14, 10, 90)))
    d.rectangle([3, 9, 28, 19], fill=hx('2f6fa8'), outline=hx('10243a')); d.rectangle([3, 19, 28, 28], fill=hx('1f4f7a'), outline=hx('10243a'))
    d.polygon([(10, 17), (21, 17), (19, 19), (12, 19)], fill=hx('6b3f2a')); d.line([(15, 10), (15, 17)], fill=hx('6b3f2a'))
    d.polygon([(15, 10), (20, 15), (15, 15)], fill=hx('ffffff')); d.rectangle([13, 10, 15, 12], fill=hx('111111'))
    save(im, 'Common', 'pirate_box.png')
    # Yamali + chest
    m = piece_mask('0111', body=10.0, r=3.6)
    m2 = m.copy()
    for yy in range(6, 14): m2[yy] = np.roll(m[yy], -1)
    im = from_masks((T, T), [(m2, hx('3b2a5c')), (edge(m2), hx('140c22'))])
    d = ImageDraw.Draw(im)
    d.rectangle([7, 20, 12, 25], fill=hx('6b5a8a')); d.line([(8, 19), (11, 26)], fill=hx('140c22'))
    d.polygon([(10, 12), (14, 14), (10, 15)], fill=hx('ff2e4c')); d.polygon([(22, 12), (18, 14), (22, 15)], fill=hx('ff2e4c'))
    d.line([(10, 19), (22, 19)], fill=hx('140c22')); d.line([(11, 20), (21, 20)], fill=hx('ffffff'))
    for x in (13, 16, 19): d.point((x, 20), fill=hx('140c22'))
    save(im, 'Common', 'yamali.png')
    im = new(); d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 31, 31], fill=hx('f0c36b'))
    for yy in (6, 14, 22):
        for xx in range(-4, 32, 8): d.arc([xx, yy - 4, xx + 8, yy + 4], 0, 180, fill=hx('c4913a'))
    d.polygon([(16, 25), (8, 16), (8, 11), (12, 8), (16, 11), (20, 8), (24, 11), (24, 16)], outline=hx('8a5a1e'))
    save(im, 'Common', 'chest_closed.png')
    im = new(); d = ImageDraw.Draw(im); d.rectangle([1, 1, 30, 30], outline=hx('ffd23f'), width=2)
    save(im, 'Common', 'chest_glow.png')


# ------------------------------------------------------------------ dragon (4x6 tiles)
def make_digits():
    f = ImageFont.truetype(FONT_B, 15)
    for n in range(1, 10):
        im = new(); d = ImageDraw.Draw(im)
        col = hx('ff2e4c') if n == 1 else hx('ffffff')
        d.ellipse([6, 4, 26, 24], fill=hx('1b1b1b'), outline=hx('ffb02e') if n == 1 else hx('9aa5b1'), width=2)
        d.polygon([(13, 23), (16, 29), (19, 23)], fill=hx('1b1b1b'))
        tw = d.textlength(str(n), font=f)
        d.text((16 - tw / 2, 5), str(n), font=f, fill=col)
        save(im, 'Common', f'num_{n}.png')


def make_dragon():
    Wd, Hd = 128, 192
    BODY, SHADE, BELLY, LINE, HORN, WING = hx('5b4b9a'), hx('43367a'), hx('f0c36b'), hx('1e1838'), hx('f3e9d2'), hx('7a68c2')
    for kind in ('awake', 'sleep'):
        im = new(Wd, Hd); d = ImageDraw.Draw(im)
        # tail
        d.polygon([(100, 170), (126, 150), (127, 120), (118, 150), (92, 160)], fill=SHADE, outline=LINE)
        d.polygon([(127, 120), (120, 108), (127, 104)], fill=HORN, outline=LINE)
        # wing
        d.polygon([(72, 72), (96, 6), (127, 30), (127, 92)], fill=WING, outline=LINE)
        for x2, y2 in ((104, 70), (118, 78)): d.line([(96, 6), (x2, y2)], fill=SHADE, width=2)
        # body
        d.ellipse([28, 66, 126, 182], fill=BODY, outline=LINE)
        # legs
        d.rounded_rectangle([44, 166, 70, 190], radius=6, fill=SHADE, outline=LINE)
        d.rounded_rectangle([88, 166, 114, 190], radius=6, fill=SHADE, outline=LINE)
        for fx in (46, 54, 62, 90, 98, 106): d.line([(fx, 186), (fx, 190)], fill=HORN, width=2)
        # belly (reaches the left edge at the chest row 96..128)
        d.polygon([(2, 94), (40, 84), (68, 104), (70, 158), (44, 170), (16, 150), (2, 130)], fill=BELLY, outline=LINE)
        for yy in (110, 124, 138, 152): d.line([(14, yy), (62, yy + 2)], fill=hx('c4913a'))
        # neck + head (mouth at y~48)
        d.polygon([(30, 96), (56, 86), (52, 50), (30, 46)], fill=BODY, outline=LINE)
        d.ellipse([10, 22, 62, 68], fill=BODY, outline=LINE)
        d.polygon([(0, 40), (24, 34), (26, 62), (0, 58)], fill=BODY, outline=LINE)
        d.line([(0, 50), (22, 52)], fill=LINE, width=2)
        d.point([(4, 43), (8, 42)], fill=LINE)
        d.polygon([(40, 26), (52, 0), (54, 28)], fill=HORN, outline=LINE)
        d.polygon([(28, 26), (32, 6), (40, 26)], fill=HORN, outline=LINE)
        for sx in range(60, 100, 10): d.polygon([(sx, 70 - (sx - 60) // 6), (sx + 5, 60 - (sx - 60) // 6), (sx + 10, 70 - (sx - 60) // 6)], fill=HORN, outline=LINE)
        if kind == 'awake':
            d.ellipse([28, 34, 42, 46], fill=hx('ff2e4c'), outline=LINE); d.rectangle([33, 36, 36, 45], fill=LINE)
            d.line([(24, 30), (44, 36)], fill=LINE, width=3)
        else:
            d.arc([28, 34, 42, 46], 20, 160, fill=LINE, width=2)
            fz = ImageFont.truetype(FONT_B, 12)
            for i, (zx, zy) in enumerate([(66, 18), (76, 8), (88, 0)]):
                d.text((zx, zy), 'z', font=fz, fill=hx('f3e9d2'))
        # arm folded on the belly (fire only comes from the mouth)
        d.polygon([(44, 124), (66, 134), (58, 152), (30, 154), (26, 142)], fill=BODY, outline=LINE)
        for cx in (30, 37, 44): d.polygon([(cx, 152), (cx + 2, 159), (cx + 5, 152)], fill=HORN, outline=LINE)
        save(im, 'Common', f'dragon_{kind}.png')


# ------------------------------------------------------------------ themes
def wall_set(key, top, front, line, top_fx=None, front_fx=None):
    for bits in SHAPES:  # L R T B open (1 = floor next to it)
        L, R, Tt, B = (b == '1' for b in bits)
        im = new(); d = ImageDraw.Draw(im)
        th = 21 if B else 32
        d.rectangle([0, 0, 31, th - 1], fill=top)
        if top_fx: top_fx(d, th, bits)
        if B:
            d.rectangle([0, th, 31, 31], fill=front)
            if front_fx: front_fx(d, th, bits)
            d.line([(0, th), (31, th)], fill=line); d.line([(0, 31), (31, 31)], fill=line)
        if L: d.line([(0, 0), (0, 31)], fill=line)
        if R: d.line([(31, 0), (31, 31)], fill=line)
        if Tt: d.line([(0, 0), (31, 0)], fill=line)
        save(im, 'Themes', key, f'wall_{bits}.png')
        if bits == '0000': save(im, 'Themes', key, 'wall.png')


def floor_set(key, base_fx, variants, alt_fx):
    for i in range(3):
        im = new(); d = ImageDraw.Draw(im); base_fx(d, i)
        if i: variants(d, i)
        save(im, 'Themes', key, 'floor.png' if i == 0 else f'floor_{i}.png')
    im = new(); d = ImageDraw.Draw(im); base_fx(d, 0); alt_fx(d); save(im, 'Themes', key, 'floor_alt.png')


def obj(key, name, fx):
    im = new(); d = ImageDraw.Draw(im)
    soft(im, lambda o: o.ellipse([5, 24, 29, 31], fill=(20, 14, 10, 90)))
    fx(d, im); save(im, 'Themes', key, f'{name}.png')


def theme_box():           # T0 cardboard
    k = 'T0_Kutu'
    def base(d, i):
        d.rectangle([0, 0, 31, 31], fill=hx('e0b47a'))
        for x in range(3, 32, 6): d.line([(x, 0), (x, 31)], fill=hx('cfa067'))
    def var(d, i):
        if i == 1: d.polygon([(16, 8), (11, 14), (14, 14), (14, 22), (18, 22), (18, 14), (21, 14)], fill=hx('d4a56c'))
        else: d.ellipse([8, 12, 20, 20], fill=hx('cc9a5a'))
    def alt(d):
        d.rectangle([5, 7, 26, 24], outline=hx('d94a3a')); d.rectangle([8, 10, 23, 21], outline=hx('d94a3a'))
    floor_set(k, base, var, alt)
    def tfx(d, th, b):
        d.rectangle([12, 0, 19, th - 1], fill=hx('c8a46e'))
        d.line([(12, 0), (12, th - 1)], fill=hx('b08a55')); d.line([(19, 0), (19, th - 1)], fill=hx('b08a55'))
    def ffx(d, th, b):
        for x in range(2, 32, 4): d.line([(x, th + 2), (x, 30)], fill=hx('8a5a2c'))
    wall_set(k, hx('d9a86a'), hx('a8743f'), hx('4a2f1d'), tfx, ffx)
    obj(k, 'obstacle', lambda d, im: (d.polygon([(4, 26), (16, 6), (28, 26)], fill=hx('c48a4c'), outline=hx('4a2f1d')),
                                      d.polygon([(16, 6), (28, 26), (16, 26)], fill=hx('a8743f'), outline=hx('4a2f1d'))))
    def staples(d, im):
        for y in (9, 18):
            d.line([(7, y), (24, y)], fill=hx('6e7780'), width=2); d.line([(7, y), (7, y + 5)], fill=hx('6e7780'), width=2); d.line([(24, y), (24, y + 5)], fill=hx('6e7780'), width=2)
            d.line([(8, y + 1), (23, y + 1)], fill=hx('c5ccd3'))
    obj(k, 'hazard', staples)


def theme_carpet():        # T1
    k = 'T1_Hali'
    def base(d, i):
        d.rectangle([0, 0, 31, 31], fill=hx('7a3b5c'))
        for y in range(2, 32, 4):
            for x in range(2 + (y // 4 % 2) * 2, 32, 4): d.point((x, y), fill=hx('5e2a46'))
    def var(d, i):
        for x, y in ([(6, 20), (18, 9)] if i == 1 else [(22, 24)]):
            d.line([(x, y), (x + 3, y - 2), (x + 5, y + 1)], fill=hx('c26a6a'))
    def alt(d):
        d.polygon([(16, 5), (27, 16), (16, 27), (5, 16)], outline=hx('c26a6a')); d.polygon([(16, 11), (21, 16), (16, 21), (11, 16)], fill=hx('c26a6a'))
    floor_set(k, base, var, alt)
    def tfx(d, th, b):
        for x, y in ((8, 9), (24, 9)) + (((16, 22),) if th == 32 else ()):
            d.ellipse([x - 2, y - 2, x + 2, y + 2], fill=hx('6b4580'))
            d.line([(x - 6, y - 6), (x - 2, y - 2)], fill=hx('3a2348')); d.line([(x + 6, y + 6), (x + 2, y + 2)], fill=hx('3a2348'))
    def ffx(d, th, b):
        for x in range(1, 32, 3): d.line([(x, 27), (x, 31)], fill=hx('c26a6a'))
    wall_set(k, hx('4a2d5c'), hx('2e1e3a'), hx('140b1c'), tfx, ffx)
    obj(k, 'obstacle', lambda d, im: (d.rectangle([9, 10, 22, 26], fill=hx('7a4f2a'), outline=hx('2e1a0c')),
                                      d.ellipse([9, 5, 22, 15], fill=hx('b07a45'), outline=hx('2e1a0c'))))
    def dust(d, im):
        rng = random.Random(7)
        pts = [(16 + int(math.cos(t) * (10 + rng.choice([-2, 0, 1]))), 15 + int(math.sin(t) * (8 + rng.choice([-2, 0, 1])))) for t in np.linspace(0, 2 * math.pi, 18, endpoint=False)]
        d.polygon(pts, fill=hx('9d98a8'), outline=hx('4a4656'))
        d.ellipse([11, 12, 14, 16], fill=hx('ffffff')); d.ellipse([18, 12, 21, 16], fill=hx('ffffff'))
        d.point([(13, 14), (20, 14)], fill=hx('1b1b1b')); d.line([(14, 19), (18, 19)], fill=hx('4a4656'))
    obj(k, 'hazard', dust)


def theme_kitchen():       # T2
    k = 'T2_Mutfak'
    def base(d, i):
        d.rectangle([0, 0, 31, 31], fill=hx('e8f1f2'))
        d.rectangle([0, 0, 15, 15], fill=hx('a9cbd6')); d.rectangle([16, 16, 31, 31], fill=hx('a9cbd6'))
        d.line([(0, 0), (31, 0)], fill=hx('6fa3b8')); d.line([(0, 0), (0, 31)], fill=hx('6fa3b8'))
    def var(d, i):
        if i == 1:
            for x, y in ((6, 22), (9, 25), (24, 6)): d.point((x, y), fill=hx('c9a27e'))
        else: d.line([(18, 4), (22, 9), (21, 13), (26, 15)], fill=hx('6fa3b8'))
    def alt(d): d.line([(3, 20), (9, 24), (12, 22), (18, 27), (22, 26)], fill=hx('1f3b4d'))
    floor_set(k, base, var, alt)
    def tfx(d, th, b):
        for x, y in ((6, 6), (20, 12), (26, 4), (12, 16)): d.point((x, y), fill=hx('b3c4cc'))
    def ffx(d, th, b):
        d.line([(16, th + 1), (16, 30)], fill=hx('1f3b4d')); d.rectangle([11, th + 4, 14, th + 5], fill=hx('e8f1f2')); d.rectangle([18, th + 4, 21, th + 5], fill=hx('e8f1f2'))
    wall_set(k, hx('d9e4e8'), hx('3b6e8f'), hx('1f3b4d'), tfx, ffx)
    obj(k, 'obstacle', lambda d, im: (d.rectangle([7, 12, 24, 27], fill=hx('f4f7f8'), outline=hx('1f3b4d')),
                                      d.ellipse([7, 7, 24, 17], fill=hx('6b3f2a'), outline=hx('1f3b4d')),
                                      d.arc([21, 14, 30, 23], 270, 90, fill=hx('1f3b4d'), width=2)))
    obj(k, 'hazard', lambda d, im: (d.ellipse([3, 8, 28, 26], fill=hx('5aa0dc'), outline=hx('2f6fa8')),
                                    d.line([(9, 13), (14, 12)], fill=hx('e8f1f2'), width=2)))


def theme_toys():          # T3
    k = 'T3_Oyuncak'
    def base(d, i):
        d.rectangle([0, 0, 31, 31], fill=hx('d9a066'))
        for kk in range(4):
            yy = kk * 8; d.line([(0, yy + 7), (31, yy + 7)], fill=hx('a86f3c'))
            cut = (kk * 11 + i * 7) % 26 + 3; d.line([(cut, yy), (cut, yy + 6)], fill=hx('a86f3c'))
    def var(d, i):
        if i == 1: d.ellipse([12, 10, 16, 13], outline=hx('a86f3c'))
        else: d.point([(3, 3), (28, 3), (3, 19), (28, 19)], fill=hx('b98149'))
    def alt(d): d.rectangle([2, 2, 29, 29], outline=hx('43aa8b'), width=2)
    floor_set(k, base, var, alt)
    def tfx(d, th, b):
        for sy in ([7] if th < 32 else [7, 21]):
            for sx in (8, 23):
                d.ellipse([sx - 4, sy - 3, sx + 3, sy + 3], fill=hx('2b7a62')); d.ellipse([sx - 4, sy - 4, sx + 3, sy + 2], fill=hx('7fd6b6'))
    def ffx(d, th, b): d.line([(0, th), (0, 31)], fill=hx('163d33'))
    wall_set(k, hx('43aa8b'), hx('2b7a62'), hx('163d33'), tfx, ffx)
    def cube(d, im):
        d.rectangle([4, 3, 27, 18], fill=hx('f9c74f'), outline=hx('4a3410')); d.rectangle([4, 18, 27, 28], fill=hx('d9a02c'), outline=hx('4a3410'))
        d.polygon([(15, 6), (11, 15), (13, 15), (15, 10), (17, 15), (19, 15)], fill=hx('f94144')); d.line([(13, 12), (17, 12)], fill=hx('f94144'))
    obj(k, 'obstacle', cube)
    def brick(d, im):
        d.rectangle([3, 8, 28, 20], fill=hx('f94144'), outline=hx('4a0f12')); d.rectangle([3, 20, 28, 27], fill=hx('b5252b'), outline=hx('4a0f12'))
        for sx in (9, 16, 23):
            d.ellipse([sx - 3, 9, sx + 3, 15], fill=hx('b5252b')); d.ellipse([sx - 3, 8, sx + 3, 13], fill=hx('ff7b7f'))
    obj(k, 'hazard', brick)


def theme_night():         # T4
    k = 'T4_Gece'
    def base(d, i):
        d.rectangle([0, 0, 31, 31], fill=hx('1e2a4a'))
        for x in (0, 11, 22): d.line([(x, 0), (x, 31)], fill=hx('141c33'))
        d.line([(1, 20 - i * 4), (10, 20 - i * 4)], fill=hx('141c33')); d.line([(12, 6 + i * 3), (21, 6 + i * 3)], fill=hx('141c33'))
    def var(d, i): d.point([(5, 4 + i * 6), (16, 26 - i * 3)], fill=hx('3d5a80'))
    def alt(d):
        d.polygon([(4, 4), (27, 2), (29, 29), (6, 27)], fill=hx('3d5a80')); d.line([(16, 3), (17, 28)], fill=hx('1e2a4a')); d.line([(5, 15), (28, 15)], fill=hx('1e2a4a'))
    floor_set(k, base, var, alt)
    def ffx(d, th, b): d.rectangle([0, 27, 31, 30], fill=hx('3d5a80'))
    wall_set(k, hx('0f1528'), hx('2a3a5e'), hx('05070f'), None, ffx)
    obj(k, 'obstacle', lambda d, im: (d.ellipse([9, 3, 22, 28], fill=hx('5c6f91'), outline=hx('141c33')),
                                      d.rectangle([9, 7, 22, 14], fill=hx('ffe8a3'), outline=hx('141c33'))))
    obj(k, 'hazard', lambda d, im: (d.rectangle([3, 3, 28, 28], fill=hx('000000'), outline=hx('3d5a80')),
                                    d.line([(4, 4), (27, 4)], fill=hx('5b7aa6'))))


def theme_cat():           # T5
    k = 'T5_Kedi'
    def base(d, i):
        d.rectangle([0, 0, 31, 31], fill=hx('7d1d24'))
        d.line([(0, 15), (31, 15)], fill=hx('5c1218')); d.line([(15, 0), (15, 31)], fill=hx('5c1218'))
    def var(d, i):
        for x, y in ([(6, 20), (18, 9)] if i == 1 else [(22, 24), (4, 6)]): d.line([(x, y), (x + 3, y - 2), (x + 5, y + 1)], fill=hx('c9a27e'))
    def alt(d):
        d.ellipse([10, 13, 21, 23], fill=hx('5c1218'))
        for x, y in ((8, 9), (13, 6), (18, 6), (23, 9)): d.ellipse([x - 2, y - 2, x + 2, y + 2], fill=hx('5c1218'))
    floor_set(k, base, var, alt)
    def tfx(d, th, b): d.line([(3, th // 2), (28, th // 2)], fill=hx('c9a27e'))
    def ffx(d, th, b):
        for x in range(4, 32, 8): d.line([(x, th + 2), (x, 29)], fill=hx('8f6c48'))
    wall_set(k, hx('e4cfa9'), hx('b08a62'), hx('3a2a22'), tfx, ffx)
    obj(k, 'obstacle', lambda d, im: (d.ellipse([3, 8, 28, 28], fill=hx('c43a2a'), outline=hx('3a2a22')),
                                      d.ellipse([8, 11, 23, 21], fill=hx('8c5a3c')),
                                      d.point([(12, 14), (17, 16), (14, 18), (19, 13)], fill=hx('5a3622'))))
    obj(k, 'hazard', lambda d, im: (d.ellipse([5, 5, 26, 26], fill=hx('ff5a36'), outline=hx('3a2a22')),
                                    d.arc([2, 8, 22, 30], 280, 30, fill=hx('3a2a22')), d.arc([10, 2, 30, 24], 120, 230, fill=hx('3a2a22')),
                                    d.line([(26, 22), (31, 30)], fill=hx('ff5a36'), width=2)))


def theme_desk():          # T6 study desk (laser level)
    k = 'T6_Masa'
    def base(d, i):
        d.rectangle([0, 0, 31, 31], fill=hx('3f8a63'))
        for v in range(0, 32, 8): d.line([(v, 0), (v, 31)], fill=hx('377a57')); d.line([(0, v), (31, v)], fill=hx('377a57'))
        d.line([(0, 0), (31, 0)], fill=hx('7cc29c')); d.line([(0, 0), (0, 31)], fill=hx('7cc29c'))
    def var(d, i):
        if i == 1:
            for x in range(2, 32, 4): d.line([(x, 28), (x, 31 if x % 8 == 2 else 30)], fill=hx('9ad1b0'))
        else: d.ellipse([18, 18, 24, 22], fill=hx('e8c06a'))
    def alt(d):
        for x, y in ((8, 10), (14, 14), (20, 9)): d.polygon([(x, y), (x + 4, y + 2), (x + 1, y + 4)], fill=hx('e8c06a'), outline=hx('8a5a2c'))
    floor_set(k, base, var, alt)
    cols = [hx('c0392b'), hx('2e86de'), hx('f1c40f'), hx('8e44ad')]
    def tfx(d, th, b):
        for y in range(3, th - 1, 4): d.line([(2, y), (29, y)], fill=hx('d8c9a5'))
    def ffx(d, th, b):
        seed = int(b, 2)
        c1, c2 = cols[seed % 4], cols[(seed + 1) % 4]
        d.rectangle([0, th, 15, 31], fill=c1); d.rectangle([16, th, 31, 31], fill=c2)
        d.line([(16, th), (16, 31)], fill=hx('2c2c2c'))
        for x in (4, 20): d.line([(x, th + 3), (x + 7, th + 3)], fill=hx('f2e8d0'))
    wall_set(k, hx('f2e8d0'), hx('c0392b'), hx('2c2c2c'), tfx, ffx)
    obj(k, 'obstacle', lambda d, im: (d.rectangle([5, 9, 26, 19], fill=hx('f78fb3'), outline=hx('3a1a28')),
                                      d.rectangle([5, 19, 26, 27], fill=hx('3b8bd9'), outline=hx('10243a')),
                                      d.line([(16, 19), (16, 27)], fill=hx('10243a'))))
    obj(k, 'hazard', lambda d, im: (d.line([(16, 18), (19, 27)], fill=hx('9aa5b1'), width=2),
                                    d.ellipse([8, 6, 24, 20], fill=hx('e74c3c'), outline=hx('5a1010')),
                                    d.ellipse([11, 8, 16, 12], fill=hx('ff9b8f'))))
    # background: desk seen from above with lamp light
    far = new(640, 360, hx('2a1d14')); d = ImageDraw.Draw(far)
    d.rectangle([0, 40, 640, 330], fill=hx('5a3d28'))
    for y in range(48, 330, 22): d.line([(0, y), (640, y + 6)], fill=hx('4c3322'))
    d.polygon([(470, 0), (640, 0), (640, 160), (520, 200)], fill=hx('6a4a30'))
    save(far, 'Themes', k, 'bg_far.png')
    near = new(640, 360); d = ImageDraw.Draw(near)
    for i, (x, col) in enumerate([(20, 'f1c40f'), (60, 'e74c3c'), (100, '2e86de')]):
        d.polygon([(x, 300), (x + 220, 250), (x + 226, 262), (x + 6, 312)], fill=hx(col), outline=hx('1b1b1b'))
        d.polygon([(x + 220, 250), (x + 240, 252), (x + 226, 262)], fill=hx('f2d7a0'), outline=hx('1b1b1b'))
    d.ellipse([480, 20, 600, 80], fill=hx('3a3f47'), outline=hx('111318'))
    save(near, 'Themes', k, 'bg_near.png')


def theme_picture():       # T7 inside the puzzle picture (dragon level)
    k = 'T7_Resim'
    def base(d, i):
        d.rectangle([0, 0, 31, 31], fill=hx('6fbf5f'))
        # jigsaw seams: right/bottom edges with a knob
        d.line([(31, 0), (31, 12)], fill=hx('4e9a43')); d.line([(31, 20), (31, 31)], fill=hx('4e9a43'))
        d.arc([27, 12, 35, 20], 90, 270, fill=hx('4e9a43'))
        d.line([(0, 31), (12, 31)], fill=hx('4e9a43')); d.line([(20, 31), (31, 31)], fill=hx('4e9a43'))
        d.arc([12, 27, 20, 35], 180, 360, fill=hx('4e9a43'))
    def var(d, i):
        pts = [(8, 9), (22, 20)] if i == 1 else [(14, 6)]
        for x, y in pts:
            d.point([(x, y - 1), (x - 1, y), (x + 1, y), (x, y + 1)], fill=hx('f7e26b')); d.point((x, y), fill=hx('e8452c'))
    def alt(d):
        for x, y in ((6, 8), (18, 6), (12, 18), (24, 20)): d.ellipse([x - 4, y - 3, x + 4, y + 3], fill=hx('d8c38f'), outline=hx('a48c58'))
    floor_set(k, base, var, alt)
    def tfx(d, th, b):
        for x in (6, 16, 26): d.ellipse([x - 2, th // 2 - 2, x + 2, th // 2 + 2], fill=hx('f0d27a'))
    def ffx(d, th, b): d.line([(0, th + 4), (31, th + 4)], fill=hx('6b4a18'))
    wall_set(k, hx('c99a3b'), hx('8a6424'), hx('3b2a10'), tfx, ffx)
    obj(k, 'obstacle', lambda d, im: (d.polygon([(5, 26), (8, 12), (16, 6), (25, 10), (28, 26)], fill=hx('9aa0a8'), outline=hx('3a3f47')),
                                      d.line([(12, 14), (17, 12)], fill=hx('c8cdd3'), width=2)))
    obj(k, 'hazard', lambda d, im: (d.polygon([(3, 16), (10, 8), (16, 14), (23, 7), (29, 16), (22, 24), (12, 22)], fill=hx('5a1e10')),
                                    d.line([(6, 16), (11, 11), (16, 17), (22, 10), (26, 16)], fill=hx('ff6b2b'), width=2)))
    far = new(640, 360, hx('8fd3f5')); d = ImageDraw.Draw(far)
    for x, y, r in ((90, 70, 30), (130, 60, 24), (420, 90, 34), (470, 80, 26)): d.ellipse([x - r, y - r // 2, x + r, y + r // 2], fill=hx('ffffff'))
    d.polygon([(0, 260), (120, 150), (220, 240), (330, 130), (470, 250), (560, 170), (640, 230), (640, 360), (0, 360)], fill=hx('7a8fbf'))
    d.polygon([(0, 300), (640, 280), (640, 360), (0, 360)], fill=hx('5aa65a'))
    for x in range(0, 640, 64):
        for y in range(0, 360, 64): d.arc([x - 6, y - 6, x + 6, y + 6], 0, 360, fill=hx('7fc0e0'))
    save(far, 'Themes', k, 'bg_far.png')
    save(new(640, 360), 'Themes', k, 'bg_near.png')


# ------------------------------------------------------------------ story screens (640x360)
def big(im, s): return im.resize((im.width * s, im.height * s), Image.NEAREST)


def piece_img(shape='1010', face='face_idle_0', hat=True, s=4):
    im = load('Player', f'body_{shape}.png'); im.alpha_composite(load('Player', f'{face}.png'))
    if hat: im.alpha_composite(load('Player', 'hat.png'))
    return big(im, s)


def puzzle_picture(w, h, dragon='dragon_awake', heart=None):
    pic = new(w, h, hx('8fd3f5')); d = ImageDraw.Draw(pic)
    d.polygon([(0, h * .7), (w * .3, h * .4), (w * .55, h * .65), (w * .8, h * .35), (w, h * .6), (w, h), (0, h)], fill=hx('7a8fbf'))
    d.rectangle([0, h * .82, w, h], fill=hx('5aa65a'))
    dr = load('Common', f'{dragon}.png'); sc = h / dr.height * .9
    dr = dr.resize((int(dr.width * sc), int(dr.height * sc)), Image.NEAREST)
    dx, dy = int(w * .5 - dr.width / 2), int(h * .08)
    pic.alpha_composite(dr, (dx, dy))
    cx, cy = dx + int(16 * sc), dy + int(112 * sc)
    if heart == 'hole': d.rectangle([cx - int(14 * sc), cy - int(14 * sc), cx + int(14 * sc), cy + int(14 * sc)], fill=hx('15151f'))
    elif heart:
        hi = heart.resize((int(32 * sc), int(32 * sc)), Image.NEAREST); pic.alpha_composite(hi, (cx - hi.width // 2, cy - hi.height // 2))
    for x in range(0, w, 24): d.line([(x, 0), (x, h)], fill=(0, 0, 0, 25))
    for y in range(0, h, 24): d.line([(0, y), (w, y)], fill=(0, 0, 0, 25))
    return pic


def table(img):
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, 640, 360], fill=hx('3b2a1d'))
    for y in range(0, 360, 30): d.line([(0, y), (640, y + 10)], fill=hx('33241a'))


def make_story():
    # title: box lid
    im = new(640, 360); table(im); d = ImageDraw.Draw(im)
    d.rectangle([80, 30, 560, 330], fill=hx('e0b47a'), outline=hx('4a2f1d'), width=4)
    pic = puzzle_picture(300, 200, 'dragon_awake', heart=None)
    im.alpha_composite(pic, (250, 90)); d.rectangle([250, 90, 550, 290], outline=hx('4a2f1d'), width=3)
    lg = new(140, 24); ImageDraw.Draw(lg).text((2, 0), 'HATPIECE', font=ImageFont.truetype(FONT_B, 20), fill=hx('d94a3a'))
    im.alpha_composite(big(lg, 2)[:, :] if False else big(lg, 2), (96, 40))
    d.text((100, 100), 'Uyuyan Ejderha', font=ImageFont.truetype(FONT_B, 18), fill=hx('4a2f1d'))
    d.text((100, 124), '1000 parça', font=ImageFont.truetype(FONT_R, 14), fill=hx('4a2f1d'))
    im.alpha_composite(piece_img(), (120, 170))
    save(im, 'Story', 'title.png')
    yam = big(load('Common', 'yamali.png'), 4)
    # 1: almost complete puzzle, the heart is missing
    im = new(640, 360); table(im)
    im.alpha_composite(puzzle_picture(360, 240, heart='hole'), (60, 50))
    im.alpha_composite(piece_img(face='face_happy'), (470, 220))
    save(im, 'Story', 'intro_1.png')
    # 2: Yamali pushes the hero off the table
    im = new(640, 360); table(im); d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 380, 260], fill=hx('5a3d28')); d.rectangle([0, 260, 380, 280], fill=hx('2a1d14'))
    im.alpha_composite(yam, (250, 140))
    hero = piece_img(face='face_dead').rotate(-35, resample=Image.NEAREST, expand=True)
    im.alpha_composite(hero, (390, 200))
    for k in range(3): d.line([(372 + k * 10, 180 + k * 8), (392 + k * 10, 186 + k * 8)], fill=hx('ffffff'), width=3)
    save(im, 'Story', 'intro_2.png')
    # 3: picture complete but wrong, the dragon wakes
    im = new(640, 360); table(im)
    im.alpha_composite(puzzle_picture(420, 280, heart=load('Common', 'yamali.png')), (110, 40))
    d = ImageDraw.Draw(im)
    for x, y in ((140, 70), (500, 90), (160, 290), (510, 280)): d.line([(x - 8, y), (x + 8, y)], fill=hx('ff2e4c'), width=3); d.line([(x, y - 8), (x, y + 8)], fill=hx('ff2e4c'), width=3)
    save(im, 'Story', 'intro_3.png')
    # 4: bottom of the box
    im = new(640, 360, hx('a8743f')); d = ImageDraw.Draw(im)
    d.polygon([(0, 0), (640, 0), (520, 90), (120, 90)], fill=hx('8a5a2c'))
    d.rectangle([120, 90, 520, 360], fill=hx('e0b47a'))
    for x in range(126, 520, 12): d.line([(x, 90), (x, 360)], fill=hx('cfa067'))
    im.alpha_composite(piece_img(face='face_idle_0', s=5), (240, 160))
    save(im, 'Story', 'intro_4.png')
    # endings
    for kind in ('t', 'b'):
        im = new(640, 360); table(im)
        im.alpha_composite(puzzle_picture(360, 240, 'dragon_sleep', heart=load('Player', 'body_1110.png')), (40, 60))
        if kind == 't':
            tr = big(load('Common', 'trash.png'), 5); im.alpha_composite(tr, (440, 170))
            im.alpha_composite(big(load('Common', 'yamali.png'), 3).rotate(20, resample=Image.NEAREST, expand=True), (460, 120))
        else:
            bx = big(load('Common', 'pirate_box.png'), 5); im.alpha_composite(bx, (430, 190))
            ya = load('Common', 'yamali.png'); dd = ImageDraw.Draw(ya)
            dd.rectangle([10, 18, 22, 21], fill=hx('3b2a5c')); dd.arc([11, 15, 21, 22], 20, 160, fill=hx('ffffff'))
            im.alpha_composite(big(ya, 3), (470, 120))
        save(im, 'Story', f'ending_{kind}.png')


if __name__ == '__main__':
    make_player(); make_ugly(); make_common(); make_digits(); make_dragon()
    for fn in (theme_box, theme_carpet, theme_kitchen, theme_toys, theme_night, theme_cat, theme_desk, theme_picture): fn()
    make_story()
    print('ok')
