"""Blob doodle stili (Bolum 1 karakterleri + hikaye ekranlari).

Referans: tombul beyaz hayvan silueti, siyah nokta gozler, kontur yok, kisa bacaklar.
Karakterler tek parca beyaz (kotu karakterler: siyah govde + beyaz kenar) kutle olarak 2x cozunurlukte
(64 px, PPU 64; ejderha 256x384) yeniden cizilir. Harita parcalari (duvar, zemin, yuva, lazer) art_doodle.py'de kalir.

    python3 Tools/art_blob.py .            # Assets/Resources/KayipParca/Art altina yazar
    python3 Tools/art_blob.py . --sheet X  # ayrica X.png'ye kontrol tablosu cikarir

Bolum 2 (Yamali'nin Intikami, platform) sprite'lari ve y_* hikaye ekranlari da burada (Art/Yamali, 2x).
Hikaye: kahraman ejderhanin BINICISI; eyer (sirtindaki yuva) sol kenarda, ejderha omzunun uzerinden
oyuncuya bakar (agzi sol ust, ates o yone cikar). Eyer kapagi = katlanmis kanat (chest_closed), acik = chest_glow.
"""
import math, os, random, sys
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.join(sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith('-') else '.', 'Assets/Resources/KayipParca/Art')
SS = 6                       # supersampling
W, K, G = 255, 0, 154        # beyaz, siyah, gri (kayip parca / ikincil)


def P(*p): return os.path.join(ROOT, *p)


def save(im, *p):
    os.makedirs(os.path.dirname(P(*p)), exist_ok=True)
    im.save(P(*p))


# ---------------------------------------------------------------- supersampled mask drawing
class Ink:
    """Tek renk maske. Koordinatlar hedef piksel uzayinda (float), icerde SS kat buyutulur."""
    def __init__(self, w, h):
        self.w, self.h = int(round(w)), int(round(h))
        self.im = Image.new('L', (self.w * SS, self.h * SS), 0)
        self.d = ImageDraw.Draw(self.im)

    def circle(self, cx, cy, r):
        self.d.ellipse([(cx - r) * SS, (cy - r) * SS, (cx + r) * SS, (cy + r) * SS], fill=255)

    def ellipse(self, cx, cy, rx, ry, rot=0):
        c, s = math.cos(math.radians(rot)), math.sin(math.radians(rot))
        pts = []
        for i in range(60):
            a = 2 * math.pi * i / 60
            x, y = rx * math.cos(a), ry * math.sin(a)
            pts.append(((cx + x * c - y * s) * SS, (cy + x * s + y * c) * SS))
        self.d.polygon(pts, fill=255)

    def rrect(self, x0, y0, x1, y1, r):
        self.d.rounded_rectangle([x0 * SS, y0 * SS, x1 * SS, y1 * SS], radius=r * SS, fill=255)

    def poly(self, pts):
        self.d.polygon([(x * SS, y * SS) for x, y in pts], fill=255)

    def line(self, pts, w):
        self.d.line([(x * SS, y * SS) for x, y in pts], fill=255, width=max(1, int(w * SS)), joint='curve')
        for x, y in (pts[0], pts[-1]): self.circle(x, y, w / 2)

    def arc(self, cx, cy, r, a0, a1, w):
        """Derece; PIL gibi saat yonu, y asagi."""
        pts = [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / 24)), cy + r * math.sin(math.radians(a0 + (a1 - a0) * i / 24))) for i in range(25)]
        self.line(pts, w)

    def goo(self, blur):
        """Yakin sekilleri yumusakca birlestirir (bulanik + esik)."""
        self.im = self.im.filter(ImageFilter.GaussianBlur(blur * SS)).point(lambda p: 255 if p >= 128 else 0)
        self.d = ImageDraw.Draw(self.im)

    def grow(self, n):
        self.im = self.im.filter(ImageFilter.MaxFilter(int(n * SS) * 2 + 1))
        self.d = ImageDraw.Draw(self.im)

    def alpha(self):
        return self.im.resize((self.w, self.h), Image.LANCZOS)


def compose(w, h, layers, bg=None):
    """layers: (Ink | L image | RGBA image, gri) sirayla. bg='black' ise opak siyah zemin."""
    out = Image.new('RGBA', (w, h), (0, 0, 0, 255) if bg == 'black' else (0, 0, 0, 0))
    for item in layers:
        if isinstance(item, tuple) and len(item) == 3:        # (RGBA, x, y)
            out.alpha_composite(item[0], (int(item[1]), int(item[2]))); continue
        ink, v = item
        a = ink.alpha() if isinstance(ink, Ink) else ink
        layer = Image.new('RGBA', (w, h), (v, v, v, 0)); layer.putalpha(a)
        out.alpha_composite(layer)
    return out


def rot(pts, cx, cy, deg):
    c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    return [(cx + (x - cx) * c - (y - cy) * s, cy + (x - cx) * s + (y - cy) * c) for x, y in pts]


def rrect_pts(x0, y0, x1, y1, r):
    pts = []
    for cx, cy, a0 in ((x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)):
        for i in range(7):
            a = math.radians(a0 + 90 * i / 6)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


# ---------------------------------------------------------------- puzzle piece blob (100 birimlik tasarim uzayi)
KNOB = [(50, 19), (81, 50), (50, 81), (19, 50)]      # K D G B


def piece_parts(ink, bits, k, ox=0, oy=0, sc=1.0, feet=True):
    def T(u, v): return (ox + (50 + (u - 50) * sc) * k, oy + (50 + (v - 50) * sc) * k)
    def R(r): return r * sc * k
    x0, y0 = T(24, 24); x1, y1 = T(76, 76); ink.rrect(x0, y0, x1, y1, R(21))
    for i, b in enumerate(bits):
        if b == '1':
            cx, cy = T(*KNOB[i]); ink.circle(cx, cy, R(13.5))
    if feet:
        for fx in (31, 55):
            x0, y0 = T(fx, 68); x1, y1 = T(fx + 14, 93); ink.rrect(x0, y0, x1, y1, R(7))


def face_ink(kind, px, ox=0, oy=0):
    k = px / 100
    f = Ink(px, px)
    def T(u, v): return (ox + u * k, oy + v * k)
    if kind in ('open', 'idle'):
        f.circle(*T(40, 48), 5.2 * k); f.circle(*T(60, 44), 6 * k)
    elif kind == 'half':
        f.ellipse(*T(40, 48), 5.2 * k, 2.6 * k); f.ellipse(*T(60, 44), 6 * k, 3 * k)
    elif kind == 'closed':
        f.line([T(35, 48), T(46, 48)], 4.5 * k); f.line([T(55, 45), T(66, 45)], 4.5 * k)
    elif kind == 'happy':
        f.arc(*T(40, 49), 6 * k, 200, 340, 4.2 * k); f.arc(*T(60, 46), 6 * k, 200, 340, 4.2 * k)
    elif kind == 'sad':
        f.circle(*T(40, 49), 5 * k); f.circle(*T(60, 47), 5.5 * k); f.arc(*T(50, 66), 7 * k, 215, 325, 3.5 * k)
    elif kind == 'dead':
        for cx, cy in ((40, 47), (60, 45)):
            f.line([T(cx - 5, cy - 5), T(cx + 5, cy + 5)], 4 * k); f.line([T(cx - 5, cy + 5), T(cx + 5, cy - 5)], 4 * k)
    return f


def hat_layers(px, ox=0, oy=0):
    """Kucuk silindir sapka, govdenin sol ust kosesinde; ust cikintiyi (x 36..64) ortmez."""
    k = px / 100
    def S(pts): return [(ox + x * k, oy + y * k) for x, y in pts]
    brim = rot(rrect_pts(9, 22, 35, 27, 2.5), 22, 25, -14)
    crown = rot(rrect_pts(14, 9, 30, 23, 3.5), 22, 25, -14)
    band = rot([(14, 15), (30, 15), (30, 18), (14, 18)], 22, 25, -14)
    o = Ink(px, px); o.poly(S(brim)); o.poly(S(crown)); o.grow(2.2 * k)
    h = Ink(px, px); h.poly(S(brim)); h.poly(S(crown))
    b = Ink(px, px); b.poly(S(band))
    return [(o, K), (h, W), (b, K)]


def piece_img(bits, px, face='idle', hat=False, fill=W, inverted=False, outline=False, feet=False, bob=0, extra=None):
    """feet: ayaklar varsayilan kapali; alt cikintiyla karisiyordu."""
    k = px / 100
    layers = []
    a = Ink(px, px); piece_parts(a, bits, k, oy=bob * k, feet=feet and not outline); a.goo(3.2 * k); layers.append((a, fill))
    if inverted or outline:
        b = Ink(px, px); piece_parts(b, bits, k, oy=bob * k, sc=.8, feet=feet and not outline); b.goo(2.6 * k); layers.append((b, K))
    if face and not outline:
        layers.append((face_ink(face, px, oy=bob * k), W if inverted else K))
    if extra: layers += extra(px, bob * k)
    if hat: layers += hat_layers(px, oy=bob * k)
    return compose(px, px, layers)


def yamali_img(px, face=True):
    def extra(px, oy):
        k = px / 100
        w = Ink(px, px); w.rrect(52 * k, (35 + oy / k) * k, 70 * k, (51 + oy / k) * k, 5 * k)    # goz bandi
        w.line([(28 * k, oy + 36 * k), (50 * k, oy + 31 * k)], 3 * k)                         # bandin ipi
        w.line([(70 * k, oy + 36 * k), (74 * k, oy + 33 * k)], 3 * k)
        if face: w.circle(40 * k, oy + 49 * k, 5.2 * k)                                      # acik goz
        p = Ink(px, px); p.circle(61 * k, oy + 43 * k, 3 * k)                                # band uzerindeki nokta
        return [(w, W), (p, K)]
    return piece_img('1011', px, face=None, inverted=True, extra=extra)


def paw_img(px, frame=0):
    k = px / 100
    bob = [0, 2, 0, -1][frame] * k
    a = Ink(px, px)
    a.circle(50 * k, 62 * k + bob, 25 * k)
    for cx, cy in ((24, 40), (41, 25), (60, 25), (77, 40)): a.circle(cx * k, cy * k - bob, 10 * k)
    a.goo(3 * k)
    p = Ink(px, px)
    p.ellipse(50 * k, 66 * k + bob, 12 * k, 9 * k)
    for cx, cy in ((26, 42), (42, 27), (60, 27), (76, 42)): p.circle(cx * k, cy * k - bob, 3.5 * k)
    return compose(px, px, [(a, W), (p, K)])


def shard_img(px, i):
    rng = random.Random(10 + i); k = px / 100
    a = Ink(px, px)
    n = rng.choice([3, 4, 4, 5])
    pts = []
    for j in range(n):
        ang = 2 * math.pi * j / n + rng.uniform(-.4, .4); rr = rng.uniform(12, 22)
        pts.append((50 * k + math.cos(ang) * rr * k, 50 * k + math.sin(ang) * rr * k))
    a.poly(pts); a.goo(4 * k)
    return compose(px, px, [(a, W)])


# ---------------------------------------------------------------- dragon (128x192 tasarim uzayi, k kat)
def dragon_img(kind, k=2.0, rider=None, hole=True):
    """Ejderha omzunun uzerinden sola (oyuncuya) bakar; govdesi saga donuk, sirtindaki eyer sol kenarda (y 96..128).
    rider: eyere oturacak RGBA (None = bos). hole: eyer yuvasi (siyah delik) cizilsin mi."""
    w, h = int(128 * k), int(192 * k)
    def T(x, y): return (x * k, y * k)
    body = Ink(w, h)
    body.ellipse(*T(62, 128), 50 * k, 54 * k)                 # govde
    body.circle(*T(26, 112), 22 * k)                          # sirt / eyer tumsegi (sol kenara tasar)
    body.poly([T(44, 86), T(74, 80), T(56, 40), T(30, 48)])   # boyun
    body.circle(*T(36, 44), 22 * k)                           # kafa
    body.rrect(*T(0, 38), *T(30, 58), 9 * k)                  # burun (agiz sol kenarda)
    body.poly([T(27, 27), T(33, 6), T(41, 27)]); body.poly([T(43, 29), T(52, 10), T(56, 31)])   # boynuzlar
    body.poly([T(70, 80), T(100, 8), T(124, 40), T(118, 88)]) # kanat (eyerin sag ustunde)
    body.ellipse(*T(100, 172), 26 * k, 10 * k, rot=-28)       # kuyruk
    body.circle(*T(124, 156), 5 * k)
    body.rrect(*T(40, 158), *T(62, 190), 8 * k); body.rrect(*T(78, 158), *T(100, 190), 8 * k)   # bacaklar
    for cx, cy in ((34, 94), (44, 86), (58, 80)): body.circle(cx * k, cy * k, 5 * k)             # sirt dikenleri
    body.goo(2.6 * k)
    ink = Ink(w, h)
    if hole: ink.rrect(*T(7, 101), *T(27, 121), 5 * k)         # eyer yuvasi
    for x0, y0, x1, y1 in ((100, 8, 112, 72), (100, 8, 120, 80)): ink.line([T(x0, y0), T(x1, y1)], 1.6 * k)   # kanat kemikleri
    for yy in (116, 130, 144, 158): ink.arc(*T(84, yy), 24 * k, -25, 25, 1.6 * k)                               # karin plakalari
    for fx in (46, 51, 56, 84, 89, 94): ink.circle(fx * k, 186 * k, 1.8 * k)                                     # parmaklar
    ink.line([T(3, 52), T(22, 51)], 2 * k)                                                                        # agiz
    if kind == 'awake':
        ink.circle(*T(32, 38), 4 * k)
        ink.line([T(22, 31), T(40, 35)], 2.6 * k)                                                                 # catik kas
        ink.poly([T(3, 52), T(20, 51), T(10, 58)])                                                                # acik agiz
    else:
        ink.arc(*T(32, 39), 5 * k, 15, 165, 2.2 * k)
    layers = [(body, W), (ink, K)]
    if kind == 'sleep':
        z = Ink(w, h)
        for (zx, zy, zs) in ((60, 24, 5), (70, 14, 7), (82, 2, 9)):
            z.line([T(zx, zy), T(zx + zs, zy), T(zx, zy + zs), T(zx + zs, zy + zs)], 1.6 * k)
        layers.append((z, W))
    if rider is not None:
        layers.append((rider, 17 * k - rider.width / 2, 111 * k - rider.height / 2))
    return compose(w, h, layers)


def seat_tiles(px=64):
    k = px / 32
    o = Ink(px, px); o.rrect(3 * k, 3 * k, 29 * k, 29 * k, 9 * k)
    i = Ink(px, px); i.rrect(5.5 * k, 5.5 * k, 26.5 * k, 26.5 * k, 7.5 * k)
    f = Ink(px, px)
    for x0 in (9, 15, 21): f.line([(x0 * k, 8 * k), ((x0 + 4) * k, 24 * k)], 1.6 * k)        # katlanmis kanat cizgileri
    closed = compose(px, px, [(o, K), (i, W), (f, K)])
    r = Ink(px, px); r.rrect(2 * k, 2 * k, 30 * k, 30 * k, 9 * k)
    r2 = Ink(px, px); r2.rrect(5 * k, 5 * k, 27 * k, 27 * k, 7 * k)
    glow = compose(px, px, [(r, W)])
    glow_a = glow.split()[3]
    from PIL import ImageChops
    glow.putalpha(ImageChops.subtract(glow_a, r2.alpha()))
    return closed, glow


# ---------------------------------------------------------------- sprite sets
PX = 64
SHAPES = [format(i, '04b') for i in range(16)]


def make_player():
    for s in SHAPES: save(piece_img(s, PX, face=None), 'Player', f'body_{s}.png')
    for i, kind in enumerate(['open', 'open', 'half', 'closed']):
        save(compose(PX, PX, [(face_ink(kind, PX), K)]), 'Player', f'face_idle_{i}.png')
    save(compose(PX, PX, [(face_ink('happy', PX), K)]), 'Player', 'face_happy.png')
    save(compose(PX, PX, [(face_ink('dead', PX), K)]), 'Player', 'face_dead.png')
    save(compose(PX, PX, hat_layers(PX)), 'Player', 'hat.png')
    for i in range(6): save(shard_img(PX, i), 'Player', f'shard_{i}.png')
    # HUD hedef ikonu: ici bos blob cerceve (haritadaki yuva doodle kalir)
    for s in SHAPES: save(piece_img(s, PX, face=None, outline=True), 'Player', f'frame_{s}.png')


def make_enemies():
    for f in range(4):
        save(piece_img('1101', PX, face='closed' if f == 2 else 'idle', inverted=True, bob=[0, 1.5, 0, -1.5][f]), 'Enemies', f'ugly_{f}.png')
        save(paw_img(PX, f), 'Enemies', f'paw_{f}.png')


def make_common():
    save(piece_img('1001', PX, face='sad', fill=G), 'Common', 'lost_sad.png')
    save(piece_img('1001', PX, face='happy', fill=G), 'Common', 'lost_happy.png')
    save(yamali_img(PX), 'Common', 'yamali.png')
    save(dragon_img('awake'), 'Common', 'dragon_awake.png')
    save(dragon_img('sleep'), 'Common', 'dragon_sleep.png')
    closed, glow = seat_tiles()
    save(closed, 'Common', 'chest_closed.png'); save(glow, 'Common', 'chest_glow.png')


# ---------------------------------------------------------------- story screens (1280x720, siyah zemin)
Z = 2                      # tasarim 640x360 birimde, cikti 2x (ekranda buyutulunce keskin kalsin)
SW, SH = 640 * Z, 360 * Z


def at(x, y): return (x * Z, y * Z)


def grid_ink():
    g = Ink(SW, SH)
    for x in range(40, 640, 40): g.line([at(x, 0), at(x, 360)], 1.2 * Z)
    for y in range(40, 360, 40): g.line([at(0, y), at(640, y)], 1.2 * Z)
    return g


def rider(k, bits='1110', face='idle', hat=True):
    return piece_img(bits, int(42 * k), face=face, hat=hat)


def scene(parts, grid=False):
    """parts: (Ink, gri) ya da (RGBA, x, y) tasarim biriminde (x, y)."""
    layers = [(grid_ink(), 60)] if grid else []
    for item in parts:
        layers.append((item[0], item[1] * Z, item[2] * Z) if len(item) == 3 else item)
    return compose(SW, SH, layers, bg='black')


def bin_img(px):
    k = px / 100
    a = Ink(px, px); a.poly([(22 * k, 30 * k), (78 * k, 30 * k), (70 * k, 96 * k), (30 * k, 96 * k)]); a.rrect(14 * k, 18 * k, 86 * k, 30 * k, 5 * k)
    a.rrect(42 * k, 8 * k, 58 * k, 20 * k, 4 * k)
    b = Ink(px, px)
    for x0 in (38, 50, 62): b.line([(x0 * k, 40 * k), ((x0 - 2) * k, 86 * k)], 2.2 * k)
    return compose(px, px, [(a, W), (b, K)])


def box_img(px):
    k = px / 100
    a = Ink(px, px); a.rrect(12 * k, 40 * k, 88 * k, 96 * k, 6 * k)
    a.poly(rot([(12 * k, 42 * k), (88 * k, 42 * k), (88 * k, 56 * k), (12 * k, 56 * k)], 12 * k, 42 * k, -28))   # acik kapak
    b = Ink(px, px)
    b.circle(50 * k, 70 * k, 7 * k)
    b.line([(40 * k, 80 * k), (60 * k, 88 * k)], 2.5 * k); b.line([(60 * k, 80 * k), (40 * k, 88 * k)], 2.5 * k)   # kurukafa
    return compose(px, px, [(a, W), (b, K)])


def make_story():
    kd = 300 * Z / 192
    hero = rider(kd)
    # baslik: uyuyan ejderha ve binicisi, yapboz izgarasi
    save(scene([(dragon_img('sleep', kd, rider=hero), 220, 30)], grid=True), 'Story', 'title.png')
    # 1: yapboz bitmek uzere, binici eksik (eyer bos)
    save(scene([(dragon_img('sleep', kd), 150, 30), (piece_img('1110', 130 * Z, face='happy', hat=True), 460, 200)], grid=True), 'Story', 'intro_1.png')
    # 2: Yamali kahramani masadan (eyerden) iter
    t = Ink(SW, SH); t.line([at(0, 262), at(400, 262)], 10 * Z); t.line([at(0, 300), at(400, 300)], 4 * Z)
    m = Ink(SW, SH)
    for i in range(3): m.line([at(395 + i * 12, 170 + i * 10), at(425 + i * 12, 178 + i * 10)], 3 * Z)
    falling = piece_img('1110', 130 * Z, face='dead', hat=True).rotate(-35, resample=Image.BICUBIC, expand=True)
    save(scene([(t, W), (m, W), (yamali_img(150 * Z), 230, 118), (falling, 410, 190)]), 'Story', 'intro_2.png')
    # 3: resim tamam ama eyerde Yamali, ejderha uyandi
    d = dragon_img('awake', kd, rider=yamali_img(int(42 * kd)))
    sp = Ink(SW, SH)
    for x, y in ((120, 70), (540, 90), (130, 290), (530, 280)): sp.line([at(x - 9, y), at(x + 9, y)], 3 * Z); sp.line([at(x, y - 9), at(x, y + 9)], 3 * Z)
    save(scene([(d, 220, 30), (sp, W)], grid=True), 'Story', 'intro_3.png')
    # 4: kutunun dibi
    b = Ink(SW, SH); b.line([at(130, 40), at(130, 330), at(510, 330), at(510, 40)], 12 * Z); b.line([at(60, 40), at(130, 40)], 12 * Z); b.line([at(510, 40), at(580, 40)], 12 * Z)
    save(scene([(b, W), (piece_img('1000', 150 * Z, face='idle', hat=True), 245, 170)]), 'Story', 'intro_4.png')
    # sonlar
    ks = 260 * Z / 192
    sleeping = dragon_img('sleep', ks, rider=rider(ks))
    happy = piece_img('1001', 80 * Z, face='happy', fill=G)
    save(scene([(sleeping, 60, 40), (bin_img(150 * Z), 440, 130), (yamali_img(90 * Z).rotate(160, resample=Image.BICUBIC, expand=True), 470, 90),
                (happy, 300, 270), (happy, 380, 262), (happy, 460, 272)], grid=True), 'Story', 'ending_t.png')
    save(scene([(sleeping, 60, 40), (box_img(170 * Z), 430, 120), (yamali_img(80 * Z), 470, 110),
                (happy, 300, 270), (happy, 380, 262), (happy, 460, 272)], grid=True), 'Story', 'ending_b.png')
    save(scene([(sleeping, 180, 40), (piece_img('1001', 70 * Z, face='sad', fill=G), 40, 280), (piece_img('0110', 60 * Z, face='sad', fill=G), 100, 292)], grid=True), 'Story', 'ending_alone.png')
    def dark_extra(px, oy):
        k = px / 100
        w = Ink(px, px); w.rrect(52 * k, 35 * k, 70 * k, 51 * k, 5 * k); w.line([(28 * k, 36 * k), (50 * k, 31 * k)], 3 * k)
        pp = Ink(px, px); pp.circle(61 * k, 43 * k, 3 * k)
        sp2 = Ink(px, px)
        for a in range(0, 360, 45):
            sp2.line([(40 * k + 7 * k * math.cos(math.radians(a)), 49 * k + 7 * k * math.sin(math.radians(a))), (40 * k + 11 * k * math.cos(math.radians(a)), 49 * k + 11 * k * math.sin(math.radians(a)))], 2 * k)
        return [(w, W), (pp, K), (sp2, W)]
    dark = piece_img('1110', 230 * Z, face=None, hat=True, inverted=True, extra=lambda px, oy: [(face_ink('open', px), W)] + dark_extra(px, oy))
    save(scene([(dark, 205, 70)]), 'Story', 'ending_dark.png')


# ---------------------------------------------------------------- yuvalar: karakterle ayni blob cerceve + parlama
def make_slots():
    """Slots/slot_XXXX: parcanin kendi blob silueti, ici bos cerceve (oyunda gri/beyaz boyanir).
    Slots/glow_XXXX: ayni cercevenin yumusak parlamasi (96 px, PPU 64)."""
    from PIL import ImageChops
    for sh in SHAPES:
        ring = piece_img(sh, PX, face=None, outline=True)
        save(ring, 'Slots', f'slot_{sh}.png')
        G = 96
        big = Image.new('L', (G, G), 0); big.paste(ring.split()[3], (16, 16))
        glow = big.filter(ImageFilter.GaussianBlur(5)).point(lambda v: min(200, int(v * 1.9)))
        out = Image.new('RGBA', (G, G), (255, 255, 255, 0)); out.putalpha(glow)
        save(out, 'Slots', f'glow_{sh}.png')


# ---------------------------------------------------------------- Bolum 2: Yamali'nin Intikami (platform), 2x
YP = 64   # 32 px karo -> 64 px (PPU 64)


def yamali_frame(feet, dy=0, k=1.0):
    """80x80 (k kat) kare: ters renkli Yamali govdesi + beyaz tombul bacaklar. feet: iki ayak ucu (80'lik uzayda)."""
    size = int(80 * k)
    body = yamali_img(int(64 * k))
    legs = Ink(size, size)
    for (hx_, hy), (fx, fy) in zip([(30, 58 + dy), (50, 58 + dy)], feet):
        legs.line([(hx_ * k, hy * k), (fx * k, fy * k)], 8 * k)
    out = compose(size, size, [(legs, W)])
    out.alpha_composite(body, (int(8 * k), int((4 + dy) * k)))
    return out


def make_yamali_player():
    save(yamali_frame([(30, 78), (50, 78)]), 'Yamali', 'yamali_idle_0.png')
    save(yamali_frame([(30, 78), (50, 78)], dy=2), 'Yamali', 'yamali_idle_1.png')
    run = [[(22, 78), (56, 78)], [(28, 78), (48, 73)], [(36, 78), (42, 76)], [(32, 73), (52, 78)]]
    for i, f in enumerate(run): save(yamali_frame(f, dy=(i % 2) * 2), 'Yamali', f'yamali_run_{i}.png')
    save(yamali_frame([(24, 72), (56, 72)], dy=-2), 'Yamali', 'yamali_jump.png')
    save(yamali_frame([(20, 78), (60, 78)]), 'Yamali', 'yamali_fall.png')


def eraser_img(k):
    """Silgi: yamalari silen bekci. Beyaz blob govde, gri uc, kizgin kaslar, tombul bacaklar. k=2: ezilmis."""
    px = YP
    body, grey, ink, legs = Ink(px, px), Ink(px, px), Ink(px, px), Ink(px, px)
    if k == 2:
        body.rrect(4, 48, 60, 62, 6); grey.rrect(40, 48, 60, 62, 6); grey.rrect(40, 48, 47, 62, 1)
        ink.line([(14, 54), (22, 56)], 2.5); ink.line([(26, 56), (34, 54)], 2.5)
        return compose(px, px, [(body, W), (grey, 150), (ink, K)])
    bob = k * 2
    body.rrect(6, 12 + bob, 58, 50 + bob, 10); body.goo(2)
    grey.rrect(40, 12 + bob, 58, 50 + bob, 10); grey.rrect(40, 12 + bob, 47, 50 + bob, 1)
    for x, y in ((18, 28), (30, 28)): ink.circle(x, y + bob, 3.6)
    ink.line([(13, 19 + bob), (22, 23 + bob)], 3); ink.line([(35, 19 + bob), (26, 23 + bob)], 3)
    ink.line([(18, 40 + bob), (30, 38 + bob)], 2.8)
    a, b = ((18, 46), (16, 62)), ((44, 46), (46, 62))
    if k == 1: a, b = ((18, 48), (22, 62)), ((44, 48), (40, 62))
    for p0, p1 in (a, b): legs.line([p0, p1], 7)
    return compose(px, px, [(legs, W), (body, W), (grey, 150), (ink, K)])


def make_yamali_tiles():
    px = YP
    # dikenler: yuvarlak uclu uc sivri
    m = Ink(px, px)
    for k in range(3):
        x0 = 4 + k * 18.8
        m.poly([(x0, 62), (x0 + 18.8, 62), (x0 + 9.4, 28)])
    m.goo(1.6)
    c = Ink(px, px)
    for k in range(3): c.line([(4 + k * 18.8 + 9.4, 42), (4 + k * 18.8 + 9.4, 58)], 2.6)
    up = compose(px, px, [(m, W), (c, K)])
    save(up, 'Yamali', 'spike_up.png'); save(up.rotate(180), 'Yamali', 'spike_down.png')
    save(up.rotate(90), 'Yamali', 'spike_left.png'); save(up.rotate(-90), 'Yamali', 'spike_right.png')
    # tek yonlu platform
    m = Ink(px, px); m.rrect(1, 1, 63, 18, 7)
    c = Ink(px, px)
    for x in (12, 32, 52): c.line([(x - 5, 9.6), (x + 5, 9.6)], 2.6)
    save(compose(px, px, [(m, W), (c, K)]), 'Yamali', 'oneway.png')
    # asansor / kayan platform parcasi
    m = Ink(px, px); m.rrect(1, 1, 63, 30, 8)
    c = Ink(px, px)
    for x in (14, 50): c.circle(x, 16, 4)
    c.line([(24, 16), (40, 16)], 3)
    save(compose(px, px, [(m, W), (c, K)]), 'Yamali', 'lift.png')
    # raylar (soluk noktalar)
    for vert in (True, False):
        m = Ink(px, px)
        for k in range(3):
            t = 10 + k * 22
            x, y = (32, t) if vert else (t, 32)
            m.circle(x, y, 3.2)
        save(compose(px, px, [(m, 80)]), 'Yamali', 'rail_v.png' if vert else 'rail_h.png')
    # cikis kapisi 64x128: beyaz cerceve, siyah ic, yildiz + ok
    w, h = px, px * 2
    m = Ink(w, h); m.rrect(4, 8, 60, 128, 26)
    inner = Ink(w, h); inner.rrect(13, 17, 51, 128, 19)
    star = Ink(w, h)
    pts = []
    for k in range(10):
        a = -math.pi / 2 + k * math.pi / 5; r = 12 if k % 2 == 0 else 5.2
        pts.append((32 + math.cos(a) * r, 52 + math.sin(a) * r))
    star.poly(pts); star.goo(1.2)
    star.line([(32, 72), (32, 100)], 3.2); star.poly([(22, 94), (42, 94), (32, 106)])
    save(compose(w, h, [(m, W), (inner, K), (star, W)]), 'Yamali', 'exit.png')
    # bayraklar
    for up_ in (False, True):
        m = Ink(px, px); m.rrect(16, 4, 20, 62, 2); m.ellipse(18, 60, 8, 4)
        f = Ink(px, px); c = Ink(px, px)
        if up_:
            f.poly([(20, 6), (58, 14), (20, 28)]); f.goo(1.2)
            c.poly([(25, 11), (46, 15), (25, 22)])
        else:
            f.poly([(20, 28), (32, 32), (30, 56), (20, 54)]); f.goo(1.2)
        save(compose(px, px, [(m, W), (f, W), (c, K)]), 'Yamali', 'flag_up.png' if up_ else 'flag_down.png')
    # yaylar
    for k, hh in enumerate((20, 40)):
        m = Ink(px, px); top = 62 - hh
        m.rrect(6, top - 8, 58, top, 4)
        pts = []
        for i in range(6):
            y = top + (62 - top) * i / 5
            pts.append((16 if i % 2 == 0 else 48, y))
        m.line(pts, 4.4)
        m.rrect(10, 60, 54, 64, 2)
        save(compose(px, px, [(m, W)]), 'Yamali', f'spring_{k}.png')
    for k in range(3): save(eraser_img(k), 'Yamali', f'eraser_{k if k < 2 else "flat"}.png')


def trash_parts(cx, base, w, h):
    t = Ink(SW, SH); t.rrect(*at(cx - w / 2, base - h), *at(cx + w / 2, base), 13 * Z)
    t.rrect(*at(cx - w / 2 - 12, base - h - 4), *at(cx + w / 2 + 12, base - h + 12), 6 * Z)
    c = Ink(SW, SH)
    for k in range(5):
        x = cx - w / 2 + 25 + k * (w - 50) / 4
        c.line([at(x, base - h + 24), at(x, base - 16)], 4 * Z)
    return [(t, W), (c, K)]


def ring_ink(cx, cy, r, w, v=W):
    g = Ink(SW, SH); g.arc(*at(cx, cy), r * Z, 0, 360, w * Z); return (g, v)


def make_yamali_story():
    ugly = lambda px: piece_img('1101', px * Z, inverted=True)
    # 1: cop kutusunda uyanis
    parts = trash_parts(320, 345, 210, 165)
    m = Ink(SW, SH)
    for x, y in ((250, 118), (392, 108), (322, 78)): m.line([at(x, y), at(x + 14, y - 14)], 3.5 * Z); m.line([at(x + 14, y), at(x, y - 14)], 3.5 * Z)
    for x, y in ((252, 186), (384, 190), (320, 176)): m.arc(*at(x, y), 9 * Z, 0, 360, 2.6 * Z)
    save(scene(parts + [(m, W), (yamali_img(150 * Z), 245, 92)]), 'Story', 'y_intro_1.png')
    # 2: intikam vakti: kutunun ustunde, etrafta yamalar
    parts = trash_parts(320, 365, 180, 150)
    arcs = Ink(SW, SH)
    for r in (150, 170): arcs.arc(*at(320, 175), r * Z, 200, 340, 3 * Z)
    standing = yamali_frame([(30, 78), (50, 78)], k=2.4)
    save(scene(parts + [(arcs, 110), (standing, 224, 20), (ugly(70), 110, 270), (ugly(70), 185, 292), (ugly(70), 440, 292), (ugly(70), 515, 270)]), 'Story', 'y_intro_2.png')
    # 3: savas: iki ordu karsi karsiya (Sapkali vs Yamali)
    div = Ink(SW, SH); div.line([at(320, 50), at(320, 320)], 3 * Z)
    vs = Ink(SW, SH)
    vs.line([at(290, 28), at(300, 56), at(310, 28)], 4 * Z)                         # V
    vs.arc(*at(338, 36), 7 * Z, 330, 90, 4 * Z); vs.arc(*at(338, 50), 7 * Z, 270, 510, 4 * Z)   # S (ust ilmek saat yonunun tersi, alt ilmek saat yonu)
    hero = piece_img('1010', 110 * Z, hat=True)
    lost = lambda: piece_img('1001', 60 * Z, face='happy', fill=G)
    yam = yamali_frame([(30, 78), (50, 78)], k=2.7)
    save(scene([(div, 90), (vs, W), (hero, 95, 125), (lost(), 45, 255), (lost(), 100, 278), (lost(), 160, 268), (lost(), 205, 246),
                (yam, 382, 40), (ugly(60), 380, 250), (ugly(60), 430, 280), (ugly(60), 490, 270), (ugly(60), 540, 252)]), 'Story', 'y_war.png')
    # 4: sonlar
    ring = ring_ink(320, 180, 150, 5)
    around = lambda n, rx, ry: [(320 + math.cos(math.radians(a)) * rx, 180 + math.sin(math.radians(a)) * ry) for a in range(0, 360, 360 // n)]
    save(scene([ring, (yamali_img(150 * Z), 245, 105)] + [(ugly(40), x - 20, y - 20) for x, y in around(8, 190, 130)]), 'Story', 'y_end_yama.png')
    save(scene([ring, (piece_img('1010', 140 * Z, face='happy', hat=True), 250, 110)] + [(piece_img('1001', 40 * Z, face='happy', fill=G), x - 20, y - 20) for x, y in around(6, 190, 130)]), 'Story', 'y_end_hero.png')
    table = Ink(SW, SH); table.rrect(*at(40, 90), *at(600, 115), 10 * Z)
    sp = Ink(SW, SH)
    for x, y in ((235, 150), (410, 165), (320, 280)): sp.line([at(x - 9, y), at(x + 9, y)], 3 * Z); sp.line([at(x, y - 9), at(x, y + 9)], 3 * Z)
    falling_h = piece_img('1010', 100 * Z, face='idle', hat=True).rotate(-25, resample=Image.BICUBIC, expand=True)
    falling_y = yamali_img(100 * Z).rotate(20, resample=Image.BICUBIC, expand=True)
    save(scene([(table, W), (sp, 200), (falling_h, 190, 200), (falling_y, 350, 225)]), 'Story', 'y_end_both.png')


# ---------------------------------------------------------------- kontrol tablosu
def sheet(path):
    names = [('Player', f'body_{s}.png') for s in ('1000', '1011', '1111', '0101')] + \
            [('Player', 'hat.png'), ('Player', 'face_idle_0.png'), ('Player', 'face_happy.png'), ('Player', 'face_dead.png'), ('Player', 'shard_0.png'),
             ('Enemies', 'ugly_0.png'), ('Enemies', 'ugly_2.png'), ('Enemies', 'paw_0.png'), ('Common', 'lost_sad.png'), ('Common', 'lost_happy.png'),
             ('Common', 'yamali.png'), ('Common', 'chest_closed.png'), ('Common', 'chest_glow.png'), ('Common', 'dragon_awake.png'), ('Common', 'dragon_sleep.png')]
    ims = [Image.open(P(*n)).convert('RGBA') for n in names]
    hero = Image.open(P('Player', 'body_1010.png')).convert('RGBA'); hero.alpha_composite(Image.open(P('Player', 'face_idle_0.png'))); hero.alpha_composite(Image.open(P('Player', 'hat.png')))
    ims.insert(0, hero)
    sc = 2
    wsum = sum(im.width * sc + 12 for im in ims); hmax = max(im.height * sc for im in ims)
    out = Image.new('RGBA', (wsum, hmax), (0, 0, 0, 255)); x = 0
    for im in ims:
        out.alpha_composite(im.resize((im.width * sc, im.height * sc), Image.NEAREST), (x, 0)); x += im.width * sc + 12
    stories = [Image.open(P('Story', f + '.png')).convert('RGBA') for f in ('title', 'intro_1', 'intro_2', 'intro_3', 'intro_4', 'ending_t', 'ending_b', 'ending_alone', 'ending_dark')]
    row = Image.new('RGBA', (SW * 3, SH * 3), (40, 40, 40, 255))
    for i, s in enumerate(stories): row.alpha_composite(s, ((i % 3) * SW, (i // 3) * SH))
    out.save(path + '_sprites.png'); row.save(path + '_story.png')


if __name__ == '__main__':
    make_player(); make_enemies(); make_common(); make_slots(); make_story()
    make_yamali_player(); make_yamali_tiles(); make_yamali_story()
    if '--sheet' in sys.argv: sheet(sys.argv[sys.argv.index('--sheet') + 1])
    print('blob art ok:', ROOT)
