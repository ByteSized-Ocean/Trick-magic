"""Original stylised animal calls and magic cues for SLEIGHT; no third-party samples.
Requires numpy and ffmpeg (or imageio-ffmpeg). Usage: python GenerateHatAudio.py OUTPUT_DIRECTORY.
"""
from pathlib import Path
import sys, wave, subprocess, shutil, hashlib, json
import numpy as np

RATE = 44100
rng = np.random.default_rng(17381)

def env(t, start, length, attack=.04):
    x = np.clip((t - start) / length, 0, 1)
    return np.where((t >= start) & (t <= start + length), np.minimum(x / attack, 1) * np.sin(np.pi * x) ** .65, 0)

def voice(t, f, rough=.1, formants=(600, 1400), brightness=.8):
    f = np.broadcast_to(f, t.shape)
    phase = 2 * np.pi * np.cumsum(f * (1 + rough * .08 * rng.standard_normal(len(t)))) / RATE
    y = np.zeros(len(t))
    for harmonic in range(1, 18):
        hz = f * harmonic
        weight = .25 / harmonic ** brightness
        for center in formants: weight = weight + np.exp(-((hz - center) / (center * .25 + 80)) ** 2) / harmonic ** .35
        y += np.sin(phase * harmonic + .12 * np.sin(t * (31 + harmonic))) * weight
    noise = rng.standard_normal(len(t))
    smooth = np.convolve(noise, np.ones(15) / 15, 'same')
    return y * (1 + rough * .5 * np.sin(2 * np.pi * 37 * t)) + smooth * rough * 3

def call(name):
    duration = {'DivineDragon': 2.1, 'Lion': 1.35, 'Tiger': 1.1, 'Bear': 1.05, 'Elephant': 1.2, 'Orca': 1.15}.get(name, .9)
    t = np.arange(int(RATE * duration)) / RATE
    if name == 'Mouse':
        f = 2200 + 900 * np.sin(t * 17)
        y = voice(t, f, .03, (2600, 5800)) * sum(env(t, s, .17) for s in [0, .27, .55])
    elif name == 'Rabbit':
        y = voice(t, 1000 + 500 * np.sin(t * 13), .12, (1900, 3100)) * (env(t, 0, .24) + .6 * env(t, .4, .25))
    elif name == 'Dove':
        y = voice(t, 290 + 35 * np.sin(t * 19), .03, (420, 720), 1.7) * (env(t, 0, .28) + .7 * env(t, .31, .38))
    elif name == 'Snake':
        noise = rng.standard_normal(len(t)); low = np.convolve(noise, np.ones(21) / 21, 'same')
        y = (noise - low) * (env(t, 0, .75) + .3 * env(t, .56, .25))
    elif name == 'Eagle':
        y = voice(t, 1750 - 1100 * np.minimum(t / .6, 1) + 65 * np.sin(t * 85), .32, (2500, 4300)) * (env(t, 0, .59) + .5 * env(t, .6, .24))
    elif name == 'Monkey':
        y = voice(t, 270 + 260 * np.sin(t * 18) ** 2, .22, (650, 1350)) * sum(env(t, s, .2) for s in [0, .27, .53])
    elif name in ('Bear', 'Lion', 'Tiger'):
        bass = {'Bear': 77, 'Lion': 55, 'Tiger': 105}[name]
        f = bass * (1.25 - .4 * t / duration) + 9 * np.sin(t * 21)
        y = voice(t, f, .85, (330, 780), .3) * env(t, 0, duration - .04, .045)
        if name == 'Bear': y *= .35 + .65 * np.sin(t * 11) ** 2
        if name == 'Tiger': y += rng.standard_normal(len(t)) * env(t, .1, .7) * .25
    elif name == 'Elephant':
        f = 220 + 190 * np.sin(np.pi * np.minimum(t / .9, 1)) + 14 * np.sin(t * 60)
        y = voice(t, f, .35, (900, 1700), .1) * env(t, 0, duration - .05, .025)
    elif name == 'Orca':
        f = 900 + 1600 * np.sin(t * 6.5) ** 2
        phase = 2 * np.pi * np.cumsum(f) / RATE
        y = np.sin(phase + 2.2 * np.sin(t * 180)) * (env(t, 0, .4) + env(t, .45, .55))
        y += np.sin(t * 24000) * np.exp(-t * 180) * 2
    elif name == 'EmperorPenguin':
        y = voice(t, 210 + 170 * np.sin(t * 12) ** 2, .45, (1000, 1900), .4) * (env(t, 0, .35) + env(t, .4, .43))
    else:
        y = voice(t, 45 + 18 * np.sin(t * 3), .95, (230, 690), .2) * env(t, 0, 2.02, .08)
        y += .5 * voice(t, 520 + 400 * t, .16, (1400, 2900)) * env(t, .3, 1.4)
    return t, y

def cue(golden):
    duration = 1.4 if golden else 2.1; t = np.arange(int(RATE * duration)) / RATE; y = np.zeros(len(t))
    notes = [880, 1108.73, 1318.51, 1760, 2217.46] if golden else [261.63, 392, 523.25, 659.25, 783.99, 1046.5]
    for i, f in enumerate(notes):
        u = np.maximum(0, t - i * .085)
        signal = np.sin(2 * np.pi * f * u) + .34 * np.sin(2 * np.pi * f * 2.76 * u) + .17 * np.sin(2 * np.pi * f * 4.13 * u)
        y += signal * np.where(t >= i * .085, np.exp(-u * (4 if golden else 2.5)) * np.minimum(u / .006, 1), 0)
    return t, y

def main(out):
    out.mkdir(parents=True, exist_ok=True)
    ffmpeg = shutil.which('ffmpeg')
    if not ffmpeg:
        import imageio_ffmpeg
        ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
    names = ['Mouse', 'Rabbit', 'Dove', 'Snake', 'Eagle', 'Monkey', 'Bear', 'Lion', 'Tiger', 'Elephant', 'Orca', 'EmperorPenguin', 'DivineDragon', 'GoldenChime', 'DragonFanfare']
    manifest = []
    for name in names:
        t, y = cue(name == 'GoldenChime') if name in ['GoldenChime', 'DragonFanfare'] else call(name)
        y *= np.minimum(t / .008, 1) * np.minimum((t[-1] - t) / .025, 1)
        y = np.tanh(y / max(.001, np.max(np.abs(y))) * 1.4) * .68
        raw = out / (name + '.wav')
        with wave.open(str(raw), 'wb') as w:
            w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE); w.writeframes((y * 32767).astype('<i2').tobytes())
        target = out / (name + '.ogg')
        subprocess.run([ffmpeg, '-nostdin', '-v', 'error', '-y', '-i', str(raw), '-c:a', 'libvorbis', '-q:a', '5', str(target)], check=True)
        raw.unlink()
        manifest.append(dict(file=target.name, kind='Original stylised animal vocalisation' if name not in ['GoldenChime', 'DragonFanfare'] else 'Original magic cue', source='Tools/GenerateHatAudio.py', third_party_samples=False, sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
        print('Prepared ' + target.name, flush=True)
    (out / 'AudioSources.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')

if __name__ == '__main__': main(Path(sys.argv[1]))
