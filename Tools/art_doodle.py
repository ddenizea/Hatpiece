"""Siyah-beyaz doodle stili (Bolum 1).

Karakter sprite'larinin cizimine dokunmaz, sadece renklerini siyah-beyaz palete cevirir.
Zemin, duvar, engel, istasyon, lazer, yuva gibi harita parcalarini doodle stilinde yeniden cizer.

    python3 Tools/art_doodle.py .

Not: Mevcut PNG'lerin uzerine yazar. Renkli eski hali git gecmisinde durur.
Karakter sprite'lari bir kez cevrildikten sonra tekrar calistirmak sorun cikarmaz
(siyah-beyaz renkler zaten paletin icinde).
"""
import math, os, random, sys
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ROOT = os.path.join(sys.argv[1] if len(sys.argv) > 1 else '.', 'Assets/Resources/KayipParca/Art')
T = 32          # tile size (px)
SS = 8          # supersampling for smooth doodle shapes
WHITE, BLACK = (255, 255, 255, 255), (0, 0, 0, 255)
THEMES = ["T0_Kutu", "T1_Hali", "T2_Mutfak", "T3_Oyuncak", "T4_Gece", "T5_Kedi", "T6_Masa", "T7_Resim"]


def P(*p): return os.path.join(ROOT, *p)


def save(im, *p):
    os.makedirs(os.path.dirname(P(*p)), exist_ok=True)
    im.save(P(*p))


# ---------------------------------------------------------------- recolouring (shapes stay exactly the same)
def g(v, a=255): return (v, v, v, a)


def recolor(path, cmap=None, fallback=None):
    """cmap: {(r,g,b): grey}. Unlisted colours go through fallback(r,g,b) -> grey (default: posterised luminance)."""
    im = Image.open(path).convert('RGBA')
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, gg, b, a = px[x, y]
            if not a: continue
            if r == gg == b and r in (0, 100, 150, 175, 255):   # already converted
                continue
            v = cmap.get((r, gg, b)) if cmap else None
            if v is None: v = (fallback or posterize)(r, gg, b)
            px[x, y] = g(v, a)
    im.save(path)


def posterize(r, gg, b):
    L = (0.299 * r + 0.587 * gg + 0.114 * b) / 255
    return 0 if L < .2 else 100 if L < .42 else 175 if L < .66 else 255


def inverted(r, gg, b):
    """Kotu karakterler: koyu kontur beyaz olur, govde siyah. Beyaz kahramanin tam tersi."""
    L = (0.299 * r + 0.587 * gg + 0.114 * b) / 255
    return 255 if L < .25 else 0 if L < .62 else 255


# ---------------------------------------------------------------- drawing helpers (supersampled)
def canvas(w=T, h=T):
    return Image.new('L', (w * SS, h * SS), 0)


def down(mask, colour=255, w=T, h=T):
    """Supersampled mask -> RGBA tile with a single grey colour (alpha = coverage)."""
    a = mask.resize((w, h), Image.LANCZOS)
    out = Image.new('RGBA', (w, h), (colour, colour, colour, 0))
    out.putalpha(a)
    return out


def layered(*parts, w=T, h=T, bg=None):
    """parts: (mask, grey) drawn in order. bg: opaque grey background or None."""
    out = Image.new('RGBA', (w, h), g(bg) if bg is not None else (0, 0, 0, 0))
    for m, v in parts:
        out.alpha_composite(down(m, v, w, h))
    return out


def s(v): return v * SS


def si(v): return int(round(v * SS))


# ---------------------------------------------------------------- walls (16 autotile variants x mark)
R_CORNER = 10


def wall_mask(bits):
    """bits = L R U D, 1 = that side is open (floor or outside). Convex corners are rounded."""
    m = canvas(); d = ImageDraw.Draw(m)
    d.rectangle([0, 0, s(T), s(T)], fill=255)
    L, R, U, D = (b == '1' for b in bits)
    r = s(R_CORNER)
    for (open1, open2, cx, cy) in ((L, U, 0, 0), (R, U, s(T), 0), (L, D, 0, s(T)), (R, D, s(T), s(T))):
        if open1 and open2:
            sx = r if cx == 0 else cx - r
            sy = r if cy == 0 else cy - r
            box = [min(cx, sx), min(cy, sy), max(cx, sx), max(cy, sy)]
            d.rectangle(box, fill=0)
            d.ellipse([sx - r, sy - r, sx + r, sy + r], fill=255)
    return m


def mark_mask(theme):
    """Small black doodle that tells the themes apart (drawn on some wall cells)."""
    m = canvas(); d = ImageDraw.Draw(m)
    c = s(T) / 2; w = si(1.6)
    if theme == 0:      # karton kutu: kivrik cizgi
        d.arc([c - s(8), c - s(3), c + s(8), c + s(6)], 200, 340, fill=255, width=w)
    elif theme == 1:    # hali: puskuller
        for dx in (-6, 0, 6):
            d.line([(c + s(dx), c - s(4)), (c + s(dx) + s(1.5), c + s(4))], fill=255, width=w)
    elif theme == 2:    # mutfak: fayans arti
        d.line([(c - s(6), c), (c + s(6), c)], fill=255, width=w); d.line([(c, c - s(6)), (c, c + s(6))], fill=255, width=w)
    elif theme == 3:    # oyuncak: lego cikintilari
        for dx in (-6.5, 6.5):
            d.ellipse([c + s(dx) - s(3.5), c - s(3.5), c + s(dx) + s(3.5), c + s(3.5)], outline=255, width=w)
    elif theme == 4:    # gece: hilal
        d.ellipse([c - s(6), c - s(6), c + s(6), c + s(6)], fill=255)
        d.ellipse([c - s(3), c - s(8), c + s(9), c + s(4)], fill=0)
    elif theme == 5:    # kedi: pati
        d.ellipse([c - s(4), c - s(1), c + s(4), c + s(6)], fill=255)
        for dx, dy in ((-5, -4), (0, -6.5), (5, -4)):
            d.ellipse([c + s(dx) - s(1.8), c + s(dy) - s(1.8), c + s(dx) + s(1.8), c + s(dy) + s(1.8)], fill=255)
    elif theme == 6:    # masa: kitap sirtlari
        for k in range(3):
            y = c - s(5) + k * s(5); d.line([(c - s(8), y), (c + s(8), y)], fill=255, width=w)
    else:               # resim: firca dalgasi
        pts = [(c - s(9) + i * s(18) / 12, c + math.sin(i / 12 * math.pi * 2) * s(3)) for i in range(13)]
        d.line(pts, fill=255, width=w, joint='curve')
    return m


def make_walls():
    for th, key in enumerate(THEMES):
        mk = mark_mask(th)
        for i in range(16):
            bits = format(i, '04b')
            wm = wall_mask(bits)
            save(layered((wm, 255)), 'Themes', key, f'wall_{bits}.png')
            # mark only where the tile is not cut away
            inside = wm.filter(ImageFilter.MinFilter(s(3) * 2 + 1)) if False else wm
            save(layered((wm, 255), (ImageChops.multiply(mk, inside), 0)), 'Themes', key, f'wallm_{bits}.png')
        save(layered((wall_mask('0000'), 255)), 'Themes', key, 'wall.png')


# ---------------------------------------------------------------- floor, corner fillets, obstacles
def dot(d, cx, cy, r, fill=255):
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=fill)


def make_floor():
    for th, key in enumerate(THEMES):
        for name, (ox, oy, v) in {'floor': (0, 0, 48), 'floor_1': (1.5, -1, 44), 'floor_2': (-1, 1.5, 40)}.items():
            m = canvas(); dot(ImageDraw.Draw(m), s(16 + ox), s(16 + oy), s(1.6))
            save(layered((m, v), bg=0), 'Themes', key, name + '.png')
        m = canvas(); d = ImageDraw.Draw(m)   # desenli zemin: kucuk carpi
        d.line([(s(13), s(13)), (s(19), s(19))], fill=255, width=si(1.4)); d.line([(s(19), s(13)), (s(13), s(19))], fill=255, width=si(1.4))
        save(layered((m, 70), bg=0), 'Themes', key, 'floor_alt.png')

        m = canvas(); d = ImageDraw.Draw(m)   # engel: beyaz cakil
        d.ellipse([s(4.5), s(6), s(27.5), s(27)], fill=255)
        h = canvas(); ImageDraw.Draw(h).arc([s(9), s(10), s(19), s(18)], 200, 290, fill=255, width=si(1.6))
        save(layered((m, 255), (h, 0)), 'Themes', key, 'obstacle.png')

        m = canvas(); d = ImageDraw.Draw(m)   # tehlike: dikenli yildiz
        pts = []
        for k in range(16):
            a = k * math.pi / 8; rr = s(13 if k % 2 == 0 else 6.5)
            pts.append((s(16) + math.cos(a) * rr, s(16) + math.sin(a) * rr))
        d.polygon(pts, fill=255)
        c = canvas(); dot(ImageDraw.Draw(c), s(16), s(16), s(2.6))
        save(layered((m, 255), (c, 0)), 'Themes', key, 'hazard.png')


def make_fillets():
    """Ic koseleri yuvarlatan beyaz dolgu (zemin karesine konur). bits = TL TR BL BR."""
    r = s(R_CORNER)
    for i in range(16):
        bits = format(i, '04b')
        m = canvas(); d = ImageDraw.Draw(m)
        for b, cx, cy in zip(bits, (0, s(T), 0, s(T)), (0, 0, s(T), s(T))):
            if b != '1': continue
            sx = r if cx == 0 else cx - r
            sy = r if cy == 0 else cy - r
            corner = canvas(); cd = ImageDraw.Draw(corner)
            cd.rectangle([min(cx, sx), min(cy, sy), max(cx, sx), max(cy, sy)], fill=255)
            cd.ellipse([sx - r, sy - r, sx + r, sy + r], fill=0)
            m = ImageChops.lighter(m, corner)
        save(layered((m, 255)), 'Common', f'fillet_{bits}.png')


# ---------------------------------------------------------------- common map pieces
def make_common():
    m = canvas(); dot(ImageDraw.Draw(m), s(16), s(16), s(2.4))
    save(layered((m, 255)), 'Common', 'path_dot.png')
    m = canvas(); ImageDraw.Draw(m).ellipse([s(10.5), s(10.5), s(21.5), s(21.5)], outline=255, width=si(1.5))
    save(layered((m, 255)), 'Common', 'path_end.png')

    m = canvas(); d = ImageDraw.Draw(m); w = si(1.7); a, b, L_ = s(2), s(30), s(7)
    for (x, y, sx, sy) in ((a, a, 1, 1), (b, a, -1, 1), (a, b, 1, -1), (b, b, -1, -1)):
        d.line([(x, y), (x + sx * L_, y)], fill=255, width=w); d.line([(x, y), (x, y + sy * L_)], fill=255, width=w)
    save(layered((m, 255)), 'Common', 'next_frame.png')

    m = canvas(); d = ImageDraw.Draw(m)   # istasyon: beyaz disk + siyah donus oklari
    d.ellipse([s(3.5), s(3.5), s(28.5), s(28.5)], fill=255)
    k = canvas(); kd = ImageDraw.Draw(k)
    kd.arc([s(9.5), s(9.5), s(22.5), s(22.5)], 200, 340, fill=255, width=si(2.2))
    kd.arc([s(9.5), s(9.5), s(22.5), s(22.5)], 20, 160, fill=255, width=si(2.2))
    kd.polygon([(s(22.5), s(12)), (s(25), s(17)), (s(20), s(16.5))], fill=255)
    kd.polygon([(s(9.5), s(20)), (s(7), s(15)), (s(12), s(15.5))], fill=255)
    save(layered((m, 255), (k, 0)), 'Common', 'station.png')

    for vert in (False, True):   # lazer: beyaz bant, siyah cekirdek / uyari: kesik cizgi
        m = canvas(); c = canvas()
        if vert:
            ImageDraw.Draw(m).rectangle([s(13.5), 0, s(18.5), s(T)], fill=255); ImageDraw.Draw(c).rectangle([s(15.3), 0, s(16.7), s(T)], fill=255)
        else:
            ImageDraw.Draw(m).rectangle([0, s(13.5), s(T), s(18.5)], fill=255); ImageDraw.Draw(c).rectangle([0, s(15.3), s(T), s(16.7)], fill=255)
        save(layered((m, 255), (c, 0)), 'Common', 'laser_v.png' if vert else 'laser_h.png')
        m = canvas(); d = ImageDraw.Draw(m)
        for k in range(4):
            t0, t1 = s(k * 8 + 1), s(k * 8 + 5)
            if vert: d.rectangle([s(15), t0, s(17), t1], fill=255)
            else: d.rectangle([t0, s(15), t1, s(17)], fill=255)
        save(layered((m, 255)), 'Common', 'laser_v_warn.png' if vert else 'laser_h_warn.png')

    for dname, (dx, dy) in {'N': (0, -1), 'E': (1, 0), 'S': (0, 1), 'W': (-1, 0)}.items():
        m = canvas(); dot(ImageDraw.Draw(m), s(16), s(16), s(7.5))
        c = canvas(); dot(ImageDraw.Draw(c), s(16 + dx * 3.5), s(16 + dy * 3.5), s(2.5))
        save(layered((m, 0), (c, 255)), 'Common', f'emitter_{dname}.png')

    m = canvas(); d = ImageDraw.Draw(m)   # ates uyarisi: kesik halka + nokta
    for k in range(8):
        d.arc([s(5), s(5), s(27), s(27)], k * 45, k * 45 + 28, fill=255, width=si(2))
    dot(d, s(16), s(16), s(3))
    save(layered((m, 255)), 'Common', 'fire_warn.png')

    for f in range(4):   # ates topu: beyaz top + gri alev kuyrugu
        m = canvas(); t = canvas(); d = ImageDraw.Draw(m); td = ImageDraw.Draw(t)
        dot(d, s(10), s(16), s(7))
        for k in range(3):
            y = s(16 + (k - 1) * 4.5); ln = s(10 + ((f + k * 2) % 4) * 2.5)
            td.line([(s(12), y), (s(12) + ln, y)], fill=255, width=si(3.2))
        save(layered((t, 150), (m, 255)), 'Common', f'fireball_{f}.png')

    for f in ('shadow_l', 'shadow_t', 'shadow_tl'):   # eski golgeler artik kullanilmiyor: bos
        save(Image.new('RGBA', (T, T), (0, 0, 0, 0)), 'Common', f + '.png')


# ---------------------------------------------------------------- slots: full outline + glow
def slot_shapes():
    for i in range(16):
        shape = format(i, '04b')
        sil = Image.open(P('Player', f'body_{shape}.png')).split()[3].point(lambda v: 255 if v else 0)
        inner = sil.filter(ImageFilter.MinFilter(5))
        ring = ImageChops.subtract(sil, inner)
        out = Image.new('RGBA', sil.size, (255, 255, 255, 0)); out.putalpha(ring)
        save(out, 'Slots', f'slot_{shape}.png')
        G = 48; big = Image.new('L', (G, G), 0); big.paste(ring, (8, 8))
        glow = big.filter(ImageFilter.GaussianBlur(2.6)).point(lambda v: min(200, int(v * 1.7)))
        out = Image.new('RGBA', (G, G), (255, 255, 255, 0)); out.putalpha(glow)
        save(out, 'Slots', f'glow_{shape}.png')


# ---------------------------------------------------------------- parallax backgrounds (barely visible)
def make_backgrounds():
    W, H = 640, 360
    for th, key in enumerate(THEMES):
        rnd = random.Random(100 + th)
        far = Image.new('RGBA', (W, H), BLACK); d = ImageDraw.Draw(far)
        for _ in range(70):
            x, y = rnd.randrange(W), rnd.randrange(H); v = rnd.choice((22, 28, 34))
            d.ellipse([x - 1, y - 1, x + 1, y + 1], fill=g(v))
        save(far, 'Themes', key, 'bg_far.png')
        near = Image.new('RGBA', (W * 2, H * 2), (0, 0, 0, 0)); d = ImageDraw.Draw(near)
        for _ in range(14):
            x, y, r = rnd.randrange(W * 2), rnd.randrange(H * 2), rnd.randrange(14, 40)
            v = g(46, 255)
            kind = rnd.randrange(3)
            if kind == 0: d.arc([x - r, y - r, x + r, y + r], rnd.randrange(360), rnd.randrange(360), fill=v, width=3)
            elif kind == 1: d.ellipse([x - r // 3, y - r // 3, x + r // 3, y + r // 3], outline=v, width=3)
            else: d.line([(x - r, y), (x + r, y + rnd.randrange(-r, r))], fill=v, width=3)
        save(near.resize((W, H), Image.LANCZOS), 'Themes', key, 'bg_near.png')


# ---------------------------------------------------------------- characters: colours only
HERO = {(244, 233, 216): 255, (27, 27, 27): 0, (232, 138, 138): 150, (217, 74, 58): 255}
UGLY = {(138, 154, 91): 0, (44, 51, 32): 255, (255, 246, 214): 255, (214, 31, 58): 0, (201, 180, 88): 100}
PAW = {(217, 195, 165): 0, (242, 155, 176): 175, (58, 42, 34): 255}
YAMALI = {(59, 42, 92): 0, (20, 12, 34): 255, (107, 90, 138): 100, (255, 46, 76): 255}
LOST = {(184, 188, 196): 175, (58, 63, 71): 0, (127, 179, 224): 255, (232, 138, 138): 100}


def make_characters():
    for f in os.listdir(P('Player')):
        if f.endswith('.png'): recolor(P('Player', f), HERO)
    for f in os.listdir(P('Enemies')):
        if f.endswith('.png'): recolor(P('Enemies', f), UGLY if f.startswith('ugly') else PAW, fallback=inverted)
    for f in ('lost_sad.png', 'lost_happy.png'):
        recolor(P('Common', f), LOST)
    recolor(P('Common', 'yamali.png'), YAMALI, fallback=inverted)
    for f in ('cable', 'chest_closed', 'chest_glow', 'console', 'dragon_awake', 'dragon_sleep', 'hook', 'pirate_box',
              'rail', 'trash', 'trolley') + tuple(f'num_{k}' for k in range(1, 10)):
        recolor(P('Common', f + '.png'))


if __name__ == '__main__':
    make_characters()
    make_walls(); make_floor(); make_fillets(); make_common(); slot_shapes(); make_backgrounds()
    print('doodle art ok:', ROOT)
