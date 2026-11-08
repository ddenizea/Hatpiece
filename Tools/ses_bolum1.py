# Bolum 1 sesleri (secilenler). Oyundaki adlar icin bkz. Assets/Resources/KayipParca/Audio; burada uretilen dosya adlari ses sayfasindaki adlardir.
"""Bolum 1 ses ornekleri: kodla sentezlenir (lisans derdi yok). python3 synth.py <cikti_klasoru>"""
import os, sys
import numpy as np
from scipy.signal import butter, sosfilt
import wave

SR = 32000
OUT = sys.argv[1] if len(sys.argv) > 1 else 'wav'
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(7)


def t(d): return np.arange(int(SR * d)) / SR
def noise(d): return rng.uniform(-1, 1, int(SR * d))


def bp(x, lo, hi, order=2): return sosfilt(butter(order, [lo, hi], btype='band', fs=SR, output='sos'), x)
def lp(x, f, order=2): return sosfilt(butter(order, f, btype='low', fs=SR, output='sos'), x)
def hp(x, f, order=2): return sosfilt(butter(order, f, btype='high', fs=SR, output='sos'), x)


def env_ad(n, a, d_tau):
    """attack (s), exponential decay time constant (s)"""
    tt = np.arange(n) / SR
    e = np.exp(-np.maximum(tt - a, 0) / d_tau)
    if a > 0: e *= np.clip(tt / a, 0, 1)
    return e


def fade(x, fi=.003, fo=.02):
    n = len(x); a, b = int(SR * fi), min(int(SR * fo), int(n * .15))
    if a: x[:a] *= np.linspace(0, 1, a)
    if b: x[-b:] *= np.linspace(1, 0, b)
    return x


def sweep(f0, f1, d, shape='exp'):
    tt = t(d)
    f = f0 * (f1 / f0) ** (tt / d) if shape == 'exp' else f0 + (f1 - f0) * tt / d
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def save(name, x, gain=.8):
    x = np.asarray(x, dtype=float)
    pk = np.max(np.abs(x)) or 1
    x = fade(x / pk * gain)
    pcm = (x * 32767).astype('<i2')
    with wave.open(os.path.join(OUT, name + '.wav'), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())


def mbox(freq, d=1.2, tau=.45, bright=1.0):
    """muzik kutusu dili: hizli atak, uzun sonum, hafif inharmonik ust tonlar"""
    tt = t(d)
    x = (np.sin(2 * np.pi * freq * tt) * env_ad(len(tt), .002, tau)
         + .35 * bright * np.sin(2 * np.pi * freq * 3.01 * tt) * env_ad(len(tt), .001, tau * .35)
         + .12 * bright * np.sin(2 * np.pi * freq * 5.43 * tt) * env_ad(len(tt), .001, tau * .15))
    click = hp(noise(.004), 3000) * .15
    x[:len(click)] += click
    return x


def note(n):  # MIDI -> Hz
    return 440 * 2 ** ((n - 69) / 12)


def mix(length, *parts):
    out = np.zeros(int(SR * length))
    for start, x in parts:
        i = int(SR * start); j = min(len(out), i + len(x))
        out[i:j] += x[:j - i]
    return out


# ---------------------------------------------------------------- oyuncu
def move():          # kalemin kagida "tik"i
    x = bp(noise(.05), 2500, 7000) * env_ad(int(SR * .05), .0005, .006)
    body = np.sin(2 * np.pi * 900 * t(.05)) * env_ad(int(SR * .05), .0005, .01) * .25
    return x + body


def rotate():        # kart kaydirma "fst"
    d = .16; n = int(SR * d); x = noise(d)
    out = np.zeros(n)
    for k, (lo, hi) in enumerate(((1500, 3500), (2500, 5500), (3500, 8000))):
        seg = bp(x, lo, hi)
        w = np.exp(-((np.arange(n) / n - (k + 1) / 4) ** 2) / .02)
        out += seg * w
    return out * np.sin(np.pi * np.arange(n) / n) ** .5


def bump():          # bogük karton "tok"
    d = .12
    s = sweep(160, 85, d) * env_ad(int(SR * d), .001, .03)
    c = lp(noise(d), 900) * env_ad(int(SR * d), .0005, .008) * .6
    return s + c


def station():       # "bloop"
    d = .2; tt = t(d)
    f = 280 * (2.6) ** (tt / d)
    x = np.sin(2 * np.pi * np.cumsum(f * (1 + .02 * np.sin(2 * np.pi * 18 * tt))) / SR)
    return x * env_ad(len(tt), .01, .07)


def death():         # kagit yirtilmasi
    d = .55; n = int(SR * d); x = bp(noise(d), 900, 6500)
    grains = np.zeros(n); pos = 0
    while pos < n:
        L = rng.integers(60, 400); a = rng.uniform(.3, 1) * (1 - pos / n) ** .7
        grains[pos:pos + L] = a
        pos += L + rng.integers(0, 250)
    grains = lp(grains, 300)
    return x * np.clip(grains, 0, None) * env_ad(n, .01, .35)


def fit():           # yapboz "TIK" (B: sadece tik)
    d = .09; n = int(SR * d)
    snap = hp(noise(d), 4000) * env_ad(n, .0002, .003)
    res = np.sin(2 * np.pi * 1850 * t(d)) * env_ad(n, .0003, .018) * .55
    body = np.sin(2 * np.pi * 420 * t(d)) * env_ad(n, .001, .025) * .45
    second = np.zeros(n); k = int(SR * .012)
    second[k:] = (hp(noise(d), 3000) * env_ad(n, .0002, .002))[:n - k] * .5
    return snap + res + body + second


def win():           # 3 notali muzik kutusu arpeji
    return mix(1.6, (0, mbox(note(84))), (.13, mbox(note(88))), (.26, mbox(note(91), 1.3, .6)))


# ---------------------------------------------------------------- dunya
def shimmer():       # yuva parliyor "sing"
    d = .7; tt = t(d); e = env_ad(len(tt), .02, .2)
    x = sum(a * np.sin(2 * np.pi * f * tt + p) for a, f, p in ((1, 2637, 0), (.6, 3951, 1), (.4, 2650, 2), (.3, 5274, 3)))
    return x * e * (1 + .3 * np.sin(2 * np.pi * 9 * tt))


def found():         # kayip parcayi bulma "pit!"
    b = sweep(900, 1500, .06) * env_ad(int(SR * .06), .002, .02)
    return mix(.2, (0, b), (.08, b * .8))


def scared():        # korkup kacma "iiih"
    d = .32; tt = t(d)
    f = 720 - 260 * tt / d + 30 * np.sin(2 * np.pi * 14 * tt)
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) + .3 * np.sin(2 * np.pi * np.cumsum(2 * f) / SR)
    return x * env_ad(len(tt), .02, .15)


def rescued():       # iki notali mutlu zil
    return mix(1.4, (0, mbox(note(88), 1, .4)), (.14, mbox(note(93), 1.2, .55)))


def squelch():       # cirkin parca adimi "vicik"
    d = .18; tt = t(d)
    f = 210 * np.exp(-tt * 9) + 70 + 25 * np.sin(2 * np.pi * 37 * tt)
    s = np.sin(2 * np.pi * np.cumsum(f) / SR) * env_ad(len(tt), .005, .05)
    w = lp(noise(d), 1200) * env_ad(len(tt), .002, .03) * .5
    return s + w


def laser():         # "vizzz"
    d = .6; tt = t(d)
    saw = 2 * ((110 * tt + .3 * np.sin(2 * np.pi * 7 * tt)) % 1) - 1
    buzz = np.sin(2 * np.pi * 880 * tt + 3 * np.sin(2 * np.pi * 110 * tt))
    x = lp(saw, 2500) * .6 + buzz * .35
    e = np.clip(tt / .05, 0, 1) * np.clip((d - tt) / .12, 0, 1)
    return x * e


def fireball():      # "firrr"
    d = .6; tt = t(d)
    x = lp(noise(d), 900) * (1 + .7 * np.sin(2 * np.pi * 28 * tt))
    rum = np.sin(2 * np.pi * np.cumsum(70 + 20 * np.sin(2 * np.pi * 3 * tt)) / SR) * .4
    return (x + rum) * np.clip(tt / .04, 0, 1) * np.exp(-tt * 2.5)


def crane():         # metal tikirti + motor
    d = 1.1; tt = t(d)
    motor = lp(2 * ((68 * tt) % 1) - 1, 400) * .35 * np.clip(tt / .1, 0, 1) * np.clip((d - tt) / .2, 0, 1)
    parts = [(0, motor)]
    for k in range(6):
        c = (np.sin(2 * np.pi * 3100 * t(.05)) + .6 * np.sin(2 * np.pi * 4700 * t(.05))) * env_ad(int(SR * .05), .0003, .008)
        parts.append((.08 + k * .16, c * .6))
    return mix(d, *parts)


def snore():         # ejderha horlamasi
    d = 2.6; tt = t(d)
    breath = np.sin(np.pi * np.clip(tt / 1.5, 0, 1)) ** 2
    growl = np.sin(2 * np.pi * np.cumsum(55 + 8 * np.sin(2 * np.pi * .7 * tt)) / SR) * (1 + .8 * np.sin(2 * np.pi * 31 * tt))
    air = lp(noise(d), 700) * .6
    out = (growl * .7 + air) * breath
    exhale = lp(noise(d), 1800) * np.sin(np.pi * np.clip((tt - 1.6) / 1.0, 0, 1)) ** 2 * .35
    return out + exhale


def roar():          # ejderha kukremesi
    d = 1.3; tt = t(d)
    f = 140 * np.exp(-tt * .9) + 45
    saw = 2 * ((np.cumsum(f) / SR) % 1) - 1
    x = lp(saw * (1 + .5 * np.sin(2 * np.pi * 23 * tt)), 1600) + bp(noise(d), 300, 2500) * .7
    return x * np.clip(tt / .08, 0, 1) * np.exp(-tt * 1.6)


def purr():          # kedi mirlamasi
    d = 2.2; tt = t(d)
    am = (.5 + .5 * np.sin(2 * np.pi * 24 * tt)) ** 3
    x = lp(noise(d), 450) * am + np.sin(2 * np.pi * 48 * tt) * am * .4
    return x * np.sin(np.pi * tt / d) ** .6


def swipe():         # pati "fss"
    d = .25; tt = t(d); x = noise(d); out = np.zeros(len(tt))
    for k in range(8):
        lo = 1200 + k * 700
        out += bp(x, lo, lo + 900) * np.exp(-((tt / d - k / 8) ** 2) / .01)
    return out * np.sin(np.pi * tt / d)


# ---------------------------------------------------------------- muzik: ana melodi (3/4 ninni, C major)
MELODY = [  # (midi, beats)
    (76, 1), (79, 1), (81, 1), (79, 2), (76, 1), (74, 1), (76, 1), (72, 1), (74, 3),
    (76, 1), (79, 1), (84, 1), (83, 2), (81, 1), (79, 1), (76, 1), (74, 1), (72, 3),
]
BASS = [48, 55, 53, 55, 48, 55, 53, 48]   # her olcude bir


def tune(instr, bpm=96, octave=0):
    beat = 60 / bpm
    total = sum(b for _, b in MELODY) * beat + 2
    parts, pos = [], 0
    for n, b in MELODY:
        parts.append((pos, instr(note(n + octave), b * beat)))
        pos += b * beat
    for i, n in enumerate(BASS):
        parts.append((i * 3 * beat, instr(note(n + 12 + octave), 3 * beat) * .45))
    return mix(total, *parts)


def i_mbox(f, d): return mbox(f, max(d, .9) + .4, .5)


def i_kalimba(f, d):
    tt = t(max(d, .8) + .3)
    x = np.sin(2 * np.pi * f * tt) * env_ad(len(tt), .003, .35) + .25 * np.sin(2 * np.pi * f * 4.1 * tt) * env_ad(len(tt), .002, .05)
    return lp(x, 3500)


def i_xylo(f, d):
    tt = t(.6)
    return (np.sin(2 * np.pi * f * tt) + .5 * np.sin(2 * np.pi * f * 3.93 * tt) * np.exp(-tt * 30)) * env_ad(len(tt), .001, .12)


SOUNDS = {
    'hareket': move, 'dondurme': rotate, 'carpma': bump, 'istasyon': station, 'olum': death,
    'yuvaya_oturma': fit, 'seviye_bitti': win,
    'yuva_parliyor': shimmer, 'kayip_bulundu': found, 'kayip_korktu': scared, 'kayip_kurtarildi': rescued,
    'cirkin_adim': squelch, 'lazer': laser, 'ates_topu': fireball, 'vinc': crane,
    'ejderha_horlama': snore, 'ejderha_kukreme': roar, 'kedi_mirlama': purr, 'pati': swipe,
}


# ---------------------------------------------------------------- v2: istenen degisiklikler
def rotate2():       # daha kisa "fst"
    d = .075; n = int(SR * d); x = noise(d)
    out = bp(x, 2000, 4500) * np.exp(-((np.arange(n) / n - .3) ** 2) / .03) + bp(x, 3500, 8000) * np.exp(-((np.arange(n) / n - .6) ** 2) / .03)
    return out * np.sin(np.pi * np.arange(n) / n) ** .5


def death_a():       # A: "puf" - parca konfetiye doner (yumusak patlama + inen minik notalar)
    pop = lp(noise(.08), 1800) * env_ad(int(SR * .08), .001, .02)
    body = sweep(320, 140, .12) * env_ad(int(SR * .12), .001, .05) * .8
    parts = [(0, pop), (0, body)]
    for k, m in enumerate((79, 76, 72)):
        b = np.sin(2 * np.pi * note(m) * t(.12)) * env_ad(int(SR * .12), .002, .04) * .35
        parts.append((.1 + k * .07, b))
    return mix(.45, *parts)


def death_b():       # B: karton buruşma + inen "vuum" (uzgun ama kisa)
    d = .5; tt = t(d)
    crump = bp(noise(d), 800, 4000) * (lp(np.abs(noise(d)) ** 4, 60) * 6) * np.exp(-tt * 7)
    womp = np.sin(2 * np.pi * np.cumsum(260 * np.exp(-tt * 2.2) + 60) / SR) * np.clip(tt / .03, 0, 1) * np.exp(-tt * 4) * .7
    return crump + womp


def win2():          # "poncik": yuvarlak, kabarcikli uc nota yukari + kucuk bir zipla
    def bub(m, d=.22):
        tt = t(d); f = note(m) * (1 + .25 * np.exp(-tt * 40))   # her nota hafif asagi kayarak oturur: "blup"
        x = np.sin(2 * np.pi * np.cumsum(f) / SR) + .15 * np.sin(2 * np.pi * np.cumsum(2 * f) / SR)
        return x * env_ad(len(tt), .006, .09)
    return mix(.85, (0, bub(72)), (.11, bub(76)), (.22, bub(79)), (.36, bub(84, .45) * 1.1))


def shimmer2():      # kisa ve net "ting": orta frekansta tek yumusak zil
    d = .3; tt = t(d)
    x = np.sin(2 * np.pi * 1046 * tt) + .25 * np.sin(2 * np.pi * 2093 * tt) * np.exp(-tt * 20)
    return x * env_ad(len(tt), .003, .09)


def rescued2():      # daha kisa iki nota
    return mix(.6, (0, mbox(note(88), .4, .12)), (.09, mbox(note(93), .5, .18)))


def laser_a():       # A: oyuncak lazer "piuuu"
    d = .28; tt = t(d)
    f = 1800 * np.exp(-tt * 9) + 260
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) + .3 * np.sin(2 * np.pi * np.cumsum(f * 1.5) / SR)
    return x * env_ad(len(tt), .002, .1)


def laser_b():       # B: yumusak guc acilmasi "vuuuin" (yukari cikan ugultu, sert vizilti yok)
    d = .45; tt = t(d)
    f = 180 + 520 * (tt / d) ** 1.5
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * (1 + .25 * np.sin(2 * np.pi * 30 * tt)) + .2 * np.sin(2 * np.pi * np.cumsum(f * 2) / SR)
    return lp(x, 2500) * np.clip(tt / .05, 0, 1) * np.clip((d - tt) / .1, 0, 1)


def fire_a():        # A: havali "fuss" (inen ruzgar)
    d = .45; tt = t(d); x = noise(d); out = np.zeros(len(tt))
    for k in range(10):
        lo = 2600 - k * 220
        out += bp(x, lo, lo + 500) * np.exp(-((tt / d - k / 10) ** 2) / .012)
    return out * np.sin(np.pi * tt / d) ** .7 + lp(noise(d), 300) * np.sin(np.pi * tt / d) * .4


def fire_b():        # B: cizgi film "puf" + kisa alev "fuu"
    d = .4; tt = t(d)
    puff = lp(noise(d), 1200) * env_ad(len(tt), .002, .03)
    fuu = bp(noise(d), 400, 1600) * np.clip((tt - .03) / .05, 0, 1) * np.exp(-np.maximum(tt - .03, 0) * 7) * .8
    tone = np.sin(2 * np.pi * np.cumsum(380 * np.exp(-tt * 3)) / SR) * np.exp(-tt * 9) * .3
    return puff + fuu + tone


def snore2():        # basi da sonu gibi: yumusak nefes al (hirlti yok), derin ver
    d = 2.6; tt = t(d)
    inhale = lp(noise(d), 1400) * np.sin(np.pi * np.clip(tt / 1.2, 0, 1)) ** 2 * .45
    rumble = lp(noise(d), 160) * np.sin(np.pi * np.clip((tt - .5) / 1.0, 0, 1)) ** 2 * .7
    exhale = lp(noise(d), 1800) * np.sin(np.pi * np.clip((tt - 1.3) / 1.2, 0, 1)) ** 2 * .55
    return inhale + rumble + exhale


V2 = {
    'hareket': (move, .45), 'dondurme': (rotate2, .8), 'carpma': (bump, .8), 'istasyon': (station, .8),
    'olum_a': (death_a, .8), 'olum_b': (death_b, .8), 'yuvaya_oturma': (fit, .8), 'seviye_bitti': (win2, .8),
    'yuva_parliyor': (shimmer2, .6), 'kayip_bulundu': (found, .8), 'kayip_korktu': (scared, .8),
    'kayip_kurtarildi': (rescued2, .8), 'cirkin_adim': (squelch, .4), 'lazer_a': (laser_a, .7), 'lazer_b': (laser_b, .7),
    'ates_topu_a': (fire_a, .8), 'ates_topu_b': (fire_b, .8), 'vinc': (crane, .8),
    'ejderha_horlama': (snore2, .8), 'ejderha_kukreme': (roar, .8), 'pati': (swipe, .8),
}


# ---------------------------------------------------------------- v3: ates topu yeni secenekler
def fire_c():        # C: "fis-pof": kisa havali ates + citirti
    d = .38; tt = t(d)
    whoosh = bp(noise(d), 700, 2500) * np.sin(np.pi * np.clip(tt / .3, 0, 1)) ** 1.5 * .7
    crack = np.zeros(len(tt))
    for k in range(7):
        i = int(rng.uniform(.03, .32) * SR); L = int(SR * .006)
        crack[i:i + L] += hp(noise(.006), 2500) * rng.uniform(.5, 1)
    return whoosh + crack


def fire_d():        # D: "vuff": ocak yanar gibi sicak, yumusak tutusma
    d = .35; tt = t(d)
    thump = np.sin(2 * np.pi * np.cumsum(110 * np.exp(-tt * 6) + 50) / SR) * env_ad(len(tt), .004, .06)
    breath = lp(noise(d), 900) * np.clip(tt / .02, 0, 1) * np.exp(-tt * 9) * .8
    return thump + breath


def fire_e():        # E: "cizz": kisa, kizgin cizirti
    d = .3; tt = t(d)
    x = bp(noise(d), 3000, 9000) * (.6 + .4 * np.abs(np.sin(2 * np.pi * 41 * tt))) * np.clip(tt / .01, 0, 1) * np.exp(-tt * 9)
    return x + lp(noise(d), 500) * np.exp(-tt * 14) * .3


V2.pop('olum_b'); V2.pop('ates_topu_a'); V2.pop('ates_topu_b')
V2.update({'ates_topu_c': (fire_c, .75), 'ates_topu_d': (fire_d, .8), 'ates_topu_e': (fire_e, .6)})


# ---------------------------------------------------------------- v4: ates topu, farkli yonler
def fire_f():        # F: kibrit cakma "fsik" + kisa alev (kagit temasina uygun)
    d = .5; tt = t(d)
    strike = bp(noise(.07), 2500, 7500) * env_ad(int(SR * .07), .002, .025)
    flare = bp(noise(d), 500, 2200) * np.clip((tt - .05) / .03, 0, 1) * np.exp(-np.maximum(tt - .05, 0) * 6) * .7
    return mix(d, (0, strike), (0, flare))


def fire_g():        # G: oyuncak "pfuuu" (begenilen lazer A gibi, ama sicak ve nefesli)
    d = .32; tt = t(d)
    f = 700 * np.exp(-tt * 6) + 180
    tone = np.sin(2 * np.pi * np.cumsum(f) / SR) * env_ad(len(tt), .003, .1) * .7
    air = bp(noise(d), 600, 2400) * env_ad(len(tt), .003, .09) * .6
    return tone + air


def fire_h():        # H: buyuk ejderha atisi "BVUUM" + arkasindan ugultu
    d = .8; tt = t(d)
    boom = np.sin(2 * np.pi * np.cumsum(90 * np.exp(-tt * 3) + 40) / SR) * env_ad(len(tt), .005, .18)
    rush = lp(noise(d), 1500) * np.clip(tt / .04, 0, 1) * np.exp(-tt * 3.2) * .9
    return boom + rush


for k in ('ates_topu_c', 'ates_topu_d', 'ates_topu_e'): V2.pop(k)
V2.update({'ates_topu_f': (fire_f, .75), 'ates_topu_g': (fire_g, .75), 'ates_topu_h': (fire_h, .8)})

if __name__ == '__main__':
    for k, (f, gn) in V2.items(): save(k, f(), gn)
    save('muzik_muzik_kutusu', tune(i_mbox), .7)
    save('muzik_kalimba', tune(i_kalimba, 88, -12), .7)
    save('muzik_ksilofon', tune(i_xylo, 104), .7)
    print('ok', len(SOUNDS) + 3)
