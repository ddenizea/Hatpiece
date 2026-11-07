"""Bolum 2 (Yama Korsanlari) art: side-view flat pixel style. PPU 32.
Usage: python3 Tools/art_bolum2.py <project_root>   (run after Tools/art.py)
Writes into Assets/Resources/KayipParca/Art/B2 and Art/Story.
"""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(__file__))
import art
from art import hx, new, save, load, soft, piece_mask, edge, from_masks, big, T, FONT_B
import numpy as np
from PIL import Image, ImageDraw, ImageFont

BODY, SHADE, BELLY, LINE, HORN, WING = hx('5b4b9a'), hx('43367a'), hx('f0c36b'), hx('1e1838'), hx('f3e9d2'), hx('7a68c2')
YAMA, YAMA_D, YAMA_L = hx('3b2a5c'), hx('140c22'), hx('6b5a8a')
RED = hx('ff2e4c')


# ------------------------------------------------------------------ dragon (side view, facing right) 128x96
def dragon_frame(wing):
    im = new(128, 96); d = ImageDraw.Draw(im)
    # tail
    d.polygon([(52, 58), (20, 66), (2, 54), (8, 70), (26, 76), (56, 70)], fill=SHADE, outline=LINE)
    d.polygon([(2, 54), (0, 46), (10, 52)], fill=HORN, outline=LINE)
    # far wing (behind body)
    tips = {0: [(58, 46), (40, 2), (78, 18)], 1: [(58, 46), (30, 20), (80, 30)], 2: [(58, 50), (36, 84), (80, 62)], 3: [(58, 46), (30, 20), (80, 30)]}[wing]
    d.polygon(tips, fill=SHADE, outline=LINE)
    # body
    d.ellipse([30, 38, 98, 76], fill=BODY, outline=LINE)
    d.polygon([(46, 62), (92, 56), (90, 72), (50, 74)], fill=BELLY, outline=LINE)
    for x in (58, 68, 78): d.line([(x, 60), (x, 72)], fill=hx('c4913a'))
    # legs tucked
    d.polygon([(50, 72), (58, 86), (66, 84), (62, 72)], fill=SHADE, outline=LINE)
    d.polygon([(76, 72), (84, 86), (92, 84), (86, 70)], fill=SHADE, outline=LINE)
    # neck + head
    d.polygon([(82, 46), (98, 30), (108, 40), (94, 58)], fill=BODY, outline=LINE)
    d.ellipse([90, 22, 120, 48], fill=BODY, outline=LINE)
    d.polygon([(110, 30), (127, 36), (127, 46), (110, 46)], fill=BODY, outline=LINE)
    d.line([(112, 42), (127, 42)], fill=LINE)
    d.polygon([(98, 24), (90, 6), (104, 22)], fill=HORN, outline=LINE)
    d.polygon([(106, 22), (104, 4), (112, 22)], fill=HORN, outline=LINE)
    d.ellipse([104, 28, 112, 36], fill=hx('ffffff'), outline=LINE); d.rectangle([108, 30, 110, 34], fill=LINE)
    for sx in range(40, 84, 9): d.polygon([(sx, 42 - (sx - 40) // 10), (sx + 4, 34 - (sx - 40) // 10), (sx + 8, 42 - (sx - 40) // 10)], fill=HORN, outline=LINE)
    # near wing
    near = {0: [(62, 46), (56, 0), (96, 20)], 1: [(62, 46), (48, 18), (98, 30)], 2: [(62, 50), (54, 90), (96, 64)], 3: [(62, 46), (48, 18), (98, 30)]}[wing]
    d.polygon(near, fill=WING, outline=LINE)
    d.line([near[0], near[1]], fill=SHADE, width=2)
    # the hero = the dragon's heart, visible on the chest
    heart = load('Player', 'body_1110.png').resize((16, 16), Image.NEAREST)
    im.alpha_composite(heart, (78, 52))
    return im


def make_dragon():
    for f in range(4): save(dragon_frame(f), 'B2', f'dragon_{f}.png')
    # a patch that sticks onto the dragon as the virus spreads
    im = new(); d = ImageDraw.Draw(im)
    d.polygon([(6, 8), (24, 5), (27, 22), (9, 26), (4, 15)], fill=YAMA, outline=YAMA_D)
    for x, y in ((8, 9), (14, 6), (20, 6), (25, 12), (26, 19), (18, 24), (10, 24), (5, 17)):
        d.line([(x - 1, y - 1), (x + 1, y + 1)], fill=YAMA_L)
    d.ellipse([12, 12, 18, 18], fill=RED)
    save(im, 'B2', 'patch.png')


# ------------------------------------------------------------------ enemies (side view, facing left)
def yama_body(size=40, shape='0111', seed=1):
    m = piece_mask(shape, body=size / 2 - 5, r=3.4)
    rng = np.random.default_rng(seed)
    for yy in range(4, 30, 6):
        sh = int(rng.integers(-1, 2)); m[yy:yy + 3] = np.roll(m[yy:yy + 3], sh, axis=1)
    return m


def make_soldier():
    for f in range(2):
        im = new(48, 48)
        m = yama_body(seed=3)
        body = from_masks((T, T), [(m, YAMA), (edge(m), YAMA_D)])
        im.alpha_composite(body, (8, 12 + f))
        d = ImageDraw.Draw(im)
        oy = 12 + f
        # eye patch strap + patch, one red eye
        d.line([(10, oy + 9), (38, oy + 15)], fill=hx('111111'), width=2)
        d.rectangle([14, oy + 9, 21, oy + 15], fill=hx('111111'))
        d.polygon([(27, oy + 12), (31, oy + 14), (27, oy + 15)], fill=RED)
        d.line([(16, oy + 21), (28, oy + 21)], fill=hx('ffffff'))
        # tricorn hat
        d.polygon([(10, oy + 4), (24, oy - 8), (38, oy + 4), (24, oy + 1)], fill=hx('1b1b1b'), outline=hx('000000'))
        d.polygon([(21, oy - 3), (24, oy - 6), (27, oy - 3), (24, oy)], fill=hx('f3e9d2'))
        # cutlass held forward (facing left)
        d.line([(10, oy + 24), (0, oy + 14 - f * 2)], fill=hx('c8cdd3'), width=3)
        d.rectangle([9, oy + 22, 13, oy + 26], fill=hx('8a6424'))
        save(im, 'B2', f'soldier_{f}.png')


def make_balloon():
    for f in range(2):
        im = new(64, 64); d = ImageDraw.Draw(im)
        cols = [hx('6b5a8a'), hx('8a9a5b'), hx('3b2a5c'), hx('c9b458')]
        d.ellipse([12, 2 + f, 52, 40 + f], fill=cols[0], outline=YAMA_D)
        for i, (x, y) in enumerate([(18, 10), (32, 8), (24, 22), (38, 24)]):
            d.rectangle([x, y + f, x + 9, y + 9 + f], fill=cols[(i + 1) % 4], outline=YAMA_D)
            d.line([(x + 1, y + f), (x + 3, y + 2 + f)], fill=hx('ffffff'))
        d.line([(20, 36 + f), (26, 48)], fill=YAMA_D); d.line([(44, 36 + f), (38, 48)], fill=YAMA_D)
        d.rectangle([24, 48, 40, 58], fill=hx('8a6424'), outline=hx('3b2a10'))
        d.ellipse([28, 42, 36, 50], fill=YAMA, outline=YAMA_D); d.point((30, 45), fill=RED)
        save(im, 'B2', f'balloon_{f}.png')


def make_ship_small():
    im = new(64, 64); d = ImageDraw.Draw(im)
    d.line([(34, 8), (34, 40)], fill=hx('5a3d28'), width=3)
    d.polygon([(36, 10), (56, 18), (56, 34), (36, 36)], fill=hx('e8dcc0'), outline=hx('3b2a10'))
    d.rectangle([42, 20, 50, 28], fill=YAMA, outline=YAMA_D)
    d.polygon([(4, 40), (60, 40), (54, 56), (12, 56)], fill=hx('7a4f2a'), outline=hx('2e1a0c'))
    d.line([(8, 46), (58, 46)], fill=hx('5a3622'))
    d.ellipse([12, 42, 20, 50], fill=hx('1b1b1b'))
    d.rectangle([2, 44, 14, 48], fill=hx('3a3a3a'))
    save(im, 'B2', 'ship_small.png')
    im = new(); d = ImageDraw.Draw(im)
    d.ellipse([9, 9, 23, 23], fill=hx('1b1b1b'), outline=hx('000000')); d.ellipse([12, 12, 16, 15], fill=hx('6b6b6b'))
    d.line([(22, 10), (25, 6)], fill=hx('8a6424'), width=2); d.point([(26, 5), (25, 4)], fill=hx('ffb02e'))
    save(im, 'B2', 'cannonball.png')


# ------------------------------------------------------------------ boss: pirate ship (256x352), cannons, captain
def make_boss():
    W, H = 256, 352
    im = new(W, H); d = ImageDraw.Draw(im)
    HULL, HULL_D, PLANK = hx('7a4f2a'), hx('2e1a0c'), hx('5a3622')
    # masts and sails
    for mx, top in ((110, 4), (190, 24)):
        d.rectangle([mx - 3, top, mx + 3, 120], fill=hx('5a3d28'), outline=HULL_D)
        d.polygon([(mx - 40, top + 14), (mx + 40, top + 14), (mx + 34, top + 76), (mx - 34, top + 76)], fill=hx('e8dcc0'), outline=hx('3b2a10'))
        for px, py in ((mx - 20, top + 30), (mx + 8, top + 46)): d.rectangle([px, py, px + 12, py + 12], fill=hx('c9b458'), outline=hx('3b2a10'))
    d.polygon([(110, 4), (140, 10), (110, 16)], fill=hx('1b1b1b'))
    d.rectangle([118, 7, 124, 12], fill=YAMA)
    # hull (bow pointing left) with three gun decks aligned to lanes (y = 93, 176, 259)
    hull = [(0, 64), (250, 56), (252, 300), (226, 336), (120, 350), (54, 330), (18, 250), (6, 140)]
    d.polygon(hull, fill=HULL, outline=HULL_D)
    for y in range(76, 345, 12): d.line([(6, y), (250, y - 2)], fill=PLANK)
    hm = Image.new('L', (W, H), 0); ImageDraw.Draw(hm).polygon(hull, fill=255, outline=255)
    a_ = np.array(im); hmask = np.array(hm) == 0; hmask[:64] = False; a_[hmask] = 0
    im.paste(Image.fromarray(a_)); d = ImageDraw.Draw(im)
    d.polygon(hull, outline=HULL_D)
    for gy in (93, 176, 259):
        d.rectangle([4, gy + 24, 250, gy + 28], fill=hx('c99a3b'), outline=HULL_D)   # gold rail under each deck
        for px in range(70, 240, 40):
            d.rectangle([px, gy - 7, px + 14, gy + 7], fill=hx('1b1208'), outline=HULL_D)
    d.rectangle([0, 52, 252, 64], fill=hx('5a3622'), outline=HULL_D)
    for px in range(8, 250, 16): d.rectangle([px, 44, px + 4, 52], fill=hx('5a3622'))
    # skull-patch emblem on the bow
    d.ellipse([20, 150, 52, 182], fill=hx('f3e9d2'), outline=HULL_D)
    d.rectangle([26, 160, 36, 168], fill=YAMA); d.ellipse([40, 160, 46, 166], fill=hx('1b1b1b')); d.line([(28, 174), (46, 174)], fill=HULL_D)
    save(im, 'B2', 'boss_ship.png')
    for state in ('closed', 'open', 'broken'):
        im = new(48, 32); d = ImageDraw.Draw(im)
        d.rectangle([20, 4, 47, 28], fill=hx('3a2414'), outline=HULL_D)
        if state == 'closed':
            d.rectangle([22, 6, 45, 26], fill=hx('8a5a2c'), outline=HULL_D); d.line([(22, 16), (45, 16)], fill=HULL_D)
        elif state == 'open':
            d.rectangle([2, 10, 30, 22], fill=hx('2b2b2b'), outline=hx('000000'))
            d.ellipse([0, 9, 8, 23], fill=RED, outline=hx('000000'))
            d.rectangle([28, 8, 44, 24], fill=hx('3a3a3a'))
        else:
            d.polygon([(14, 12), (26, 10), (30, 20), (18, 24)], fill=hx('2b2b2b'))
            for k, (x, y) in enumerate(((10, 4), (16, 0), (6, 0))): d.ellipse([x, y, x + 6, y + 6], fill=hx('7a7a7a'))
        save(im, 'B2', f'cannon_{state}.png')
    for state in ('idle', 'throw', 'hurt'):
        im = new(64, 64)
        m = yama_body(size=44, shape='1111', seed=9)
        bd = from_masks((T, T), [(m, hx('8a1c2b')), (edge(m), YAMA_D)])
        im.alpha_composite(big(bd, 2).resize((52, 52), Image.NEAREST), (6, 12))
        d = ImageDraw.Draw(im)
        d.polygon([(4, 18), (32, -2), (60, 18), (32, 12)], fill=hx('1b1b1b'), outline=hx('000000'))
        d.rectangle([28, 4, 36, 10], fill=hx('f3e9d2'))
        d.line([(10, 26), (56, 34)], fill=hx('111111'), width=3)
        if state != 'hurt': d.ellipse([16, 24, 30, 36], fill=YAMA, outline=YAMA_D)  # the big eye patch
        d.ellipse([38, 28, 46, 36], fill=hx('ffffff')); d.rectangle([40, 30, 43, 34], fill=RED)
        d.arc([20, 38, 46, 52], 20, 160, fill=hx('ffffff'), width=2)
        if state == 'throw':
            d.ellipse([0, 34, 14, 48], fill=hx('1b1b1b'))
        else:
            d.arc([0, 38, 12, 52], 90, 270, fill=hx('c8cdd3'), width=3)
        if state == 'hurt':
            for x, y in ((14, 22), (54, 20)): d.line([(x - 4, y), (x + 4, y)], fill=hx('ffd23f'), width=2); d.line([(x, y - 4), (x, y + 4)], fill=hx('ffd23f'), width=2)
        save(im, 'B2', f'captain_{state}.png')
    im = new(); d = ImageDraw.Draw(im)
    d.ellipse([8, 10, 24, 22], fill=YAMA, outline=YAMA_D); d.line([(0, 14), (8, 15)], fill=hx('111111'), width=2); d.line([(24, 15), (31, 14)], fill=hx('111111'), width=2)
    save(im, 'B2', 'eyepatch.png')


# ------------------------------------------------------------------ fx + backgrounds
def make_fx():
    im = new(); ImageDraw.Draw(im).rectangle([0, 0, 31, 31], fill=hx('ffffff', 255)); save(im, 'B2', 'white.png')
    im = new(); d = ImageDraw.Draw(im)
    d.polygon([(16, 4), (28, 18), (20, 18), (20, 28), (12, 28), (12, 18), (4, 18)], fill=hx('ffffff'), outline=hx('4a4a4a')); save(im, 'B2', 'arrow_up.png')
    im = new(); d = ImageDraw.Draw(im)
    for r, c in ((14, 'ff8a2e'), (10, 'ffd23f'), (5, 'ffffff')): d.ellipse([16 - r, 16 - r, 16 + r, 16 + r], fill=hx(c))
    save(im, 'B2', 'boom.png')
    im = new(); d = ImageDraw.Draw(im)
    for x, y, r in ((10, 14, 5), (18, 10, 6), (22, 18, 5), (14, 20, 4)): d.ellipse([x - r, y - r, x + r, y + r], fill=hx('9b7fd0'))
    save(im, 'B2', 'spore.png')


def make_backgrounds():
    W, H = 640, 360
    sky = new(W, H, hx('8fd3f5')); d = ImageDraw.Draw(sky)
    d.rectangle([0, 230, W, H], fill=hx('a8dcf2'))
    for x in range(0, W, 64):
        for y in range(0, H, 64): d.arc([x - 6, y - 6, x + 6, y + 6], 0, 360, fill=hx('7fc0e0'))
    save(sky, 'B2', 'bg_sky.png')
    far = new(W, H); d = ImageDraw.Draw(far)
    pts = [(0, 250)]
    hs = [170, 230, 180, 240, 175, 225, 185, 235]
    for i, x in enumerate(range(0, W + 1, 80)): pts.append((x, hs[i % 8]))
    pts += [(W, 250), (W, H), (0, H)]
    d.polygon(pts, fill=hx('7a8fbf'))
    # tiny Korsan Gemisi box silhouette far away
    d.rectangle([470, 196, 530, 226], fill=hx('2f6fa8')); d.polygon([(495, 176), (515, 190), (495, 190)], fill=hx('ffffff'))
    save(far, 'B2', 'bg_far.png')
    near = new(W, H); d = ImageDraw.Draw(near)
    d.rectangle([0, 318, W, H], fill=hx('5aa65a'))
    for x in range(0, W, 32): d.arc([x + 12, 314, x + 20, 322], 180, 360, fill=hx('4e9a43'))
    for x, y, r in ((80, 60, 30), (110, 52, 24), (360, 90, 34), (400, 82, 26), (560, 40, 22)):
        d.ellipse([x - r, y - r // 2, x + r, y + r // 2], fill=hx('ffffff'))
    save(near, 'B2', 'bg_near.png')


def make_panels():
    fnt = ImageFont.truetype(FONT_B, 22)
    # 1: the pirate box bursts open, patches pour out
    im = new(640, 360); art.table(im); d = ImageDraw.Draw(im)
    d.rectangle([200, 180, 440, 320], fill=hx('2f6fa8'), outline=hx('10243a'), width=4)
    d.polygon([(200, 180), (260, 60), (500, 90), (440, 180)], fill=hx('1f4f7a'), outline=hx('10243a'))
    d.polygon([(300, 210), (340, 250), (300, 250)], fill=hx('ffffff')); d.line([(300, 210), (300, 260)], fill=hx('6b3f2a'), width=3)
    sold = big(load('B2', 'soldier_0.png'), 2)
    for x, y in ((240, 100), (330, 40), (420, 110), (150, 150), (480, 160)): im.alpha_composite(sold, (x, y))
    art.save(im, 'Story', 'b2_intro_1.png')
    # 2: the dragon wakes up, the hero shines in its chest
    im = new(640, 360, hx('8fd3f5')); d = ImageDraw.Draw(im)
    d.polygon([(0, 260), (160, 160), (320, 240), (480, 150), (640, 230), (640, 360), (0, 360)], fill=hx('7a8fbf'))
    d.rectangle([0, 320, 640, 360], fill=hx('5aa65a'))
    dr = big(load('B2', 'dragon_0.png'), 3); im.alpha_composite(dr, (130, 40))
    for r in (40, 30):
        soft(im, lambda o, r=r: o.ellipse([130 + 86 * 3 - r, 40 + 60 * 3 - r, 130 + 86 * 3 + r, 40 + 60 * 3 + r], outline=(255, 210, 63, 160), width=3))
    art.save(im, 'Story', 'b2_intro_2.png')
    # end: the captain's patch lands on the dragon, eyes turn purple
    im = new(640, 360, hx('2a1a3a')); d = ImageDraw.Draw(im)
    dr = dragon_frame(1)
    p = load('B2', 'patch.png').resize((24, 24), Image.NEAREST)
    for x, y in ((40, 44), (64, 40), (30, 62), (84, 50)): dr.alpha_composite(p, (x, y))
    dd = ImageDraw.Draw(dr); dd.ellipse([104, 28, 112, 36], fill=hx('b06cff'), outline=LINE); dd.rectangle([108, 30, 110, 34], fill=LINE)
    im.alpha_composite(big(dr, 3), (60, 40))
    im.alpha_composite(big(load('B2', 'captain_idle.png'), 2), (480, 200))
    art.save(im, 'Story', 'b2_end.png')
    # to be continued
    im = new(640, 360, hx('0c0a12')); d = ImageDraw.Draw(im)
    im.alpha_composite(big(dr, 2), (192, 60))
    art.save(im, 'Story', 'b3_soon.png')


if __name__ == '__main__':
    make_dragon(); make_soldier(); make_balloon(); make_ship_small(); make_boss(); make_fx(); make_backgrounds(); make_panels()
    print('ok')
