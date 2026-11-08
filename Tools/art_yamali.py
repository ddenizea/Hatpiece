"""Bolum 2 (Yamali'nin Intikami) icin siyah-beyaz doodle gorselleri. Ege ayni dosya adlariyla uzerine yazabilir.

    python3 Tools/art_yamali.py .

Cikti: Assets/Resources/KayipParca/Art/Yamali/*.png ve Art/Story/y_*.png
"""
import math, os, random, sys
from PIL import Image, ImageDraw, ImageFilter, ImageChops, ImageFont

ROOT = os.path.join(sys.argv[1] if len(sys.argv) > 1 else '.', 'Assets/Resources/KayipParca/Art')
SS = 8
FONT = '/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf'


def P(*p): return os.path.join(ROOT, *p)


def save(im, *p):
    os.makedirs(os.path.dirname(P(*p)), exist_ok=True)
    im.save(P(*p))


def canvas(w, h): return Image.new('L', (w * SS, h * SS), 0)
def s(v): return v * SS
def si(v): return int(round(v * SS))


def layered(w, h, *parts, bg=None):
    out = Image.new('RGBA', (w, h), (bg, bg, bg, 255) if bg is not None else (0, 0, 0, 0))
    for m, v in parts:
        a = m.resize((w, h), Image.LANCZOS)
        layer = Image.new('RGBA', (w, h), (v, v, v, 0)); layer.putalpha(a)
        out.alpha_composite(layer)
    return out


# ---------------------------------------------------------------- Yamali (pixel karakter + cop bacaklar)
def yamali_frames():
    body = Image.open(P('Common', 'yamali.png')).convert('RGBA')

    def frame(name, legs, dy=0, body_img=body):
        im = Image.new('RGBA', (40, 40), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        for (x0, y0, x1, y1) in legs:      # beyaz cop bacak + siyah kontur
            d.line([(x0, y0), (x1, y1)], fill=(0, 0, 0, 255), width=4)
        for (x0, y0, x1, y1) in legs:
            d.line([(x0, y0), (x1, y1)], fill=(255, 255, 255, 255), width=2)
            d.rectangle([x1 - 2, y1 - 1, x1 + 1, y1], fill=(255, 255, 255, 255))   # ayak
        im.alpha_composite(body_img, (4, 2 + dy))
        save(im, 'Yamali', name + '.png')

    stand = [(15, 33, 15, 39), (25, 33, 25, 39)]
    frame('yamali_idle_0', stand)
    frame('yamali_idle_1', [(15, 34, 15, 39), (25, 34, 25, 39)], dy=1)
    run = [
        [(15, 33, 11, 38), (25, 33, 28, 39)],
        [(15, 33, 14, 39), (25, 33, 24, 37)],
        [(15, 33, 18, 39), (25, 33, 21, 38)],
        [(15, 33, 16, 37), (25, 33, 26, 39)],
    ]
    for k, legs in enumerate(run): frame(f'yamali_run_{k}', legs, dy=(k % 2))
    frame('yamali_jump', [(15, 33, 12, 36), (25, 33, 28, 36)], dy=-1)
    frame('yamali_fall', [(15, 33, 10, 39), (25, 33, 30, 39)])


# ---------------------------------------------------------------- harita parcalari
def spikes():
    m = canvas(32, 32); d = ImageDraw.Draw(m)
    for k in range(3):
        x0 = 2 + k * 9.4
        d.polygon([(s(x0), s(31)), (s(x0 + 9.4), s(31)), (s(x0 + 4.7), s(14))], fill=255)
    c = canvas(32, 32); cd = ImageDraw.Draw(c)
    for k in range(3):
        x0 = 2 + k * 9.4
        cd.line([(s(x0 + 4.7), s(20)), (s(x0 + 4.7), s(29))], fill=255, width=si(1.4))
    up = layered(32, 32, (m, 255), (c, 0))
    save(up, 'Yamali', 'spike_up.png')
    save(up.rotate(180), 'Yamali', 'spike_down.png')
    save(up.rotate(90), 'Yamali', 'spike_left.png')     # sivri uc sola bakar
    save(up.rotate(-90), 'Yamali', 'spike_right.png')


def platforms():
    m = canvas(32, 32); d = ImageDraw.Draw(m)
    d.rounded_rectangle([s(.5), s(.5), s(31.5), s(9)], radius=s(3.5), fill=255)
    c = canvas(32, 32); cd = ImageDraw.Draw(c)
    for x in (6, 16, 26): cd.line([(s(x - 2.5), s(4.8)), (s(x + 2.5), s(4.8))], fill=255, width=si(1.3))
    save(layered(32, 32, (m, 255), (c, 0)), 'Yamali', 'oneway.png')

    m = canvas(32, 32); d = ImageDraw.Draw(m)       # asansor: kalin beyaz govde, siyah percinler
    d.rounded_rectangle([s(.5), s(.5), s(31.5), s(15)], radius=s(4), fill=255)
    c = canvas(32, 32); cd = ImageDraw.Draw(c)
    for x in (7, 25):
        cd.ellipse([s(x - 2), s(6), s(x + 2), s(10)], fill=255)
    cd.line([(s(12), s(8)), (s(20), s(8))], fill=255, width=si(1.5))
    save(layered(32, 32, (m, 255), (c, 0)), 'Yamali', 'lift.png')

    for vert in (True, False):
        m = canvas(32, 32); d = ImageDraw.Draw(m)
        for k in range(3):
            t = 5 + k * 11
            x, y = (16, t) if vert else (t, 16)
            d.ellipse([s(x - 1.6), s(y - 1.6), s(x + 1.6), s(y + 1.6)], fill=255)
        save(layered(32, 32, (m, 80)), 'Yamali', 'rail_v.png' if vert else 'rail_h.png')


def exit_door():
    w, h = 32, 64
    m = canvas(w, h); d = ImageDraw.Draw(m)
    d.rounded_rectangle([s(2), s(4), s(30), s(64)], radius=s(13), fill=255)
    inner = canvas(w, h); idd = ImageDraw.Draw(inner)
    idd.rounded_rectangle([s(6.5), s(8.5), s(25.5), s(64)], radius=s(9.5), fill=255)
    star = canvas(w, h); sd = ImageDraw.Draw(star)
    cx, cy = 16, 26; pts = []
    for k in range(10):
        a = -math.pi / 2 + k * math.pi / 5; r = 6 if k % 2 == 0 else 2.6
        pts.append((s(cx + math.cos(a) * r), s(cy + math.sin(a) * r)))
    sd.polygon(pts, fill=255)
    sd.line([(s(16), s(36)), (s(16), s(52))], fill=255, width=si(1.6))
    sd.polygon([(s(11), s(47)), (s(21), s(47)), (s(16), s(53))], fill=255)
    save(layered(w, h, (m, 255), (inner, 0), (star, 255)), 'Yamali', 'exit.png')


def flags():
    for up in (False, True):
        m = canvas(32, 32); d = ImageDraw.Draw(m)
        d.rectangle([s(8), s(2), s(10), s(31)], fill=255)
        d.ellipse([s(5), s(28), s(13), s(32)], fill=255)
        f = canvas(32, 32); fd = ImageDraw.Draw(f); c = canvas(32, 32); cd = ImageDraw.Draw(c)
        if up:
            fd.polygon([(s(10), s(3)), (s(29), s(7)), (s(10), s(14))], fill=255)
            cd.polygon([(s(12.5), s(5.5)), (s(23), s(7.5)), (s(12.5), s(11))], fill=255)   # siyah yama deseni
        else:
            fd.polygon([(s(10), s(14)), (s(16), s(16)), (s(15), s(28)), (s(10), s(27))], fill=255)
        save(layered(32, 32, (m, 255), (f, 255), (c, 0)), 'Yamali', 'flag_up.png' if up else 'flag_down.png')


def springs():
    for k, h in enumerate((10, 20)):
        m = canvas(32, 32); d = ImageDraw.Draw(m)
        top = 31 - h
        d.rounded_rectangle([s(3), s(top - 4), s(29), s(top)], radius=s(2), fill=255)
        pts = []
        n = 5
        for i in range(n + 1):
            y = top + (31 - top) * i / n
            pts.append((s(8 if i % 2 == 0 else 24), s(y)))
        d.line(pts, fill=255, width=si(2.2), joint='curve')
        d.rectangle([s(5), s(30), s(27), s(32)], fill=255)
        save(layered(32, 32, (m, 255)), 'Yamali', f'spring_{k}.png')


def eraser():
    """Silgi: yamalari silen bekci. Beyaz/gri iki renkli gövde, kizgin kaslar, kisa bacaklar."""
    for k in range(3):
        flat = k == 2
        m = canvas(32, 32); d = ImageDraw.Draw(m); g = canvas(32, 32); gd = ImageDraw.Draw(g)
        e = canvas(32, 32); ed = ImageDraw.Draw(e)
        if flat:
            d.rounded_rectangle([s(2), s(24), s(30), s(31)], radius=s(3), fill=255)
            gd.rounded_rectangle([s(19), s(24), s(30), s(31)], radius=s(3), fill=255)
            ed.line([(s(7), s(27)), (s(11), s(28))], fill=255, width=si(1.4)); ed.line([(s(13), s(28)), (s(17), s(27))], fill=255, width=si(1.4))
            save(layered(32, 32, (m, 255), (g, 150), (e, 0)), 'Yamali', 'eraser_flat.png')
            continue
        bob = k
        d.rounded_rectangle([s(3), s(6 + bob), s(29), s(25 + bob)], radius=s(4), fill=255)
        gd.rounded_rectangle([s(19), s(6 + bob), s(29), s(25 + bob)], radius=s(4), fill=255)
        gd.rectangle([s(19), s(6 + bob), s(22), s(25 + bob)], fill=255)
        for x, y in ((9, 14), (15, 14)):
            ed.ellipse([s(x - 1.8), s(y + bob - 1.8), s(x + 1.8), s(y + bob + 1.8)], fill=255)
        ed.line([(s(6.5), s(9.5 + bob)), (s(11), s(11.5 + bob))], fill=255, width=si(1.5))
        ed.line([(s(17.5), s(9.5 + bob)), (s(13), s(11.5 + bob))], fill=255, width=si(1.5))
        ed.line([(s(9), s(20 + bob)), (s(15), s(19 + bob))], fill=255, width=si(1.4))
        legs = canvas(32, 32); ld = ImageDraw.Draw(legs)
        a, b = ((9, 23), (8, 31)), ((22, 23), (23, 31))
        if k == 1: a, b = ((9, 24), (11, 31)), ((22, 24), (20, 31))
        for (p0, p1) in (a, b): ld.line([(s(p0[0]), s(p0[1])), (s(p1[0]), s(p1[1]))], fill=255, width=si(2))
        save(layered(32, 32, (legs, 255), (m, 255), (g, 150), (e, 0)), 'Yamali', f'eraser_{k}.png')


# ---------------------------------------------------------------- hikaye resimleri (640x360, doodle)
W, H = 640, 360


def scene():
    return Image.new('L', (W * 2, H * 2), 0), 2


def paste_sprite(img, path, cx, cy, scale, recolor=None):
    sp = Image.open(P(*path.split('/'))).convert('RGBA')
    sp = sp.resize((int(sp.width * scale), int(sp.height * scale)), Image.NEAREST)
    lum = sp.convert('L'); a = sp.split()[3]
    img.paste(lum, (int(cx - sp.width / 2), int(cy - sp.height / 2)), a)


def dots(img, k, seed):
    d = ImageDraw.Draw(img); rnd = random.Random(seed)
    for _ in range(90):
        x, y = rnd.randrange(img.width), rnd.randrange(img.height)
        d.ellipse([x - 2, y - 2, x + 2, y + 2], fill=40)


def text(img, t, y, size=34, fill=255):
    d = ImageDraw.Draw(img); f = ImageFont.truetype(FONT, size)
    w = d.textlength(t, font=f); d.text(((img.width - w) / 2, y), t, font=f, fill=fill)


def trash_can(d, cx, base, w=300, h=330, lid_angle=0):
    d.rounded_rectangle([cx - w / 2, base - h, cx + w / 2, base], radius=26, fill=255)
    for k in range(5):
        x = cx - w / 2 + 50 + k * (w - 100) / 4
        d.line([(x, base - h + 40), (x, base - 30)], fill=0, width=8)


def story_images():
    # 1) cop kutusunda uyanis
    img, k = scene(); dots(img, k, 1); d = ImageDraw.Draw(img)
    trash_can(d, 640, 690, 420, 330)
    d.rounded_rectangle([380, 330, 900, 372], radius=18, fill=255)          # yana kaymis kapak
    d.polygon([(420, 330), (860, 300), (870, 330)], fill=0)
    for x, y, r in ((520, 340, 30), (760, 345, 24), (640, 330, 18)):        # cop: kagit toplari
        d.ellipse([x - r, y - r, x + r, y + r], outline=255, width=6)
    paste_sprite(img, 'Common/yamali.png', 640, 300, 6)
    for x, y in ((560, 210), (720, 200), (650, 170)):                       # "z z" uyku cizgileri kapandi, sinir isaretleri
        d.line([(x, y), (x + 26, y - 26)], fill=255, width=7); d.line([(x + 26, y), (x, y - 26)], fill=255, width=7)
    save(img.resize((W, H), Image.LANCZOS).convert('RGBA'), 'Story', 'y_intro_1.png')

    # 2) intikam vakti: kutunun ustunde, arkada yamalarin golgeleri
    img, k = scene(); dots(img, k, 2); d = ImageDraw.Draw(img)
    trash_can(d, 640, 760, 360, 300)
    paste_sprite(img, 'Yamali/yamali_idle_0.png', 640, 330, 9)
    for i, (x, y) in enumerate(((240, 560), (380, 600), (900, 600), (1040, 560))):
        paste_sprite(img, f'Enemies/ugly_{i % 4}.png', x, y, 4)
    for r in (300, 340):
        d.arc([640 - r, 330 - r, 640 + r, 330 + r], 200, 340, fill=110, width=6)
    save(img.resize((W, H), Image.LANCZOS).convert('RGBA'), 'Story', 'y_intro_2.png')

    # 3) savas: iki ordu karsi karsiya
    img, k = scene(); dots(img, k, 3); d = ImageDraw.Draw(img)
    d.line([(640, 80), (640, 640)], fill=90, width=6)
    paste_sprite(img, 'Player/body_1010.png', 300, 360, 6)
    paste_sprite(img, 'Player/face_idle_0.png', 300, 360, 6)
    paste_sprite(img, 'Player/hat.png', 300, 360, 6)
    for i, (x, y) in enumerate(((150, 520), (260, 560), (380, 540), (470, 500))):
        paste_sprite(img, 'Common/lost_happy.png', x, y, 3)
    paste_sprite(img, 'Yamali/yamali_idle_0.png', 980, 340, 6)
    for i, (x, y) in enumerate(((820, 500), (920, 560), (1040, 540), (1140, 510))):
        paste_sprite(img, f'Enemies/ugly_{i % 4}.png', x, y, 3)
    text(img, 'VS', 300, 90)
    save(img.resize((W, H), Image.LANCZOS).convert('RGBA'), 'Story', 'y_war.png')

    # 4) sonlar
    img, k = scene(); dots(img, k, 4); d = ImageDraw.Draw(img)                # Yamali kazanir: kalpte o oturuyor
    d.ellipse([340, 60, 940, 660], outline=255, width=10)
    paste_sprite(img, 'Common/yamali.png', 640, 360, 9)
    for a in range(0, 360, 45):
        x, y = 640 + math.cos(math.radians(a)) * 380, 360 + math.sin(math.radians(a)) * 260
        paste_sprite(img, f'Enemies/ugly_{a // 45 % 4}.png', x, y, 2)
    save(img.resize((W, H), Image.LANCZOS).convert('RGBA'), 'Story', 'y_end_yama.png')

    img, k = scene(); dots(img, k, 5); d = ImageDraw.Draw(img)                # Kalp kazanir
    d.ellipse([340, 60, 940, 660], outline=255, width=10)
    paste_sprite(img, 'Player/body_1010.png', 640, 360, 8)
    paste_sprite(img, 'Player/face_happy.png', 640, 360, 8)
    paste_sprite(img, 'Player/hat.png', 640, 360, 8)
    for a in range(0, 360, 60):
        x, y = 640 + math.cos(math.radians(a)) * 380, 360 + math.sin(math.radians(a)) * 260
        paste_sprite(img, 'Common/lost_happy.png', x, y, 2)
    save(img.resize((W, H), Image.LANCZOS).convert('RGBA'), 'Story', 'y_end_hero.png')

    img, k = scene(); dots(img, k, 6); d = ImageDraw.Draw(img)                # ikisi de duser
    d.rounded_rectangle([80, 180, 1200, 230], radius=20, fill=255)            # masa kenari
    for x in (420, 860):
        d.line([(x, 260), (x - 30, 330)], fill=120, width=6)
    paste_sprite(img, 'Player/body_1010.png', 500, 430, 5)
    paste_sprite(img, 'Player/face_idle_0.png', 500, 430, 5)
    paste_sprite(img, 'Common/yamali.png', 780, 470, 5)
    for x, y in ((470, 300), (820, 330), (640, 560)):
        d.line([(x - 18, y), (x + 18, y)], fill=200, width=6); d.line([(x, y - 18), (x, y + 18)], fill=200, width=6)
    save(img.resize((W, H), Image.LANCZOS).convert('RGBA'), 'Story', 'y_end_both.png')


if __name__ == '__main__':
    yamali_frames(); spikes(); platforms(); exit_door(); flags(); springs(); eraser()
    story_images()
    print('yamali art ok:', ROOT)
