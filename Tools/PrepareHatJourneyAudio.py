"""Prepare licensed animal calls plus original departure Foley. Requires numpy and ffmpeg.
Usage: python PrepareHatJourneyAudio.py OUTPUT_DIRECTORY [EXISTING_AudioSources.json]
"""
from pathlib import Path
import hashlib, json, shutil, subprocess, sys, urllib.parse, urllib.request
import numpy as np

RATE = 44100
rng = np.random.default_rng(4817)
ffmpeg = shutil.which('ffmpeg')
if not ffmpeg:
    import imageio_ffmpeg
    ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()

def commons(filename):
    digest = hashlib.md5(filename.encode()).hexdigest()
    return 'https://upload.wikimedia.org/wikipedia/commons/' + digest[0] + '/' + digest[:2] + '/' + urllib.parse.quote(filename)

def write(out, name, pcm):
    pcm = np.asarray(pcm, dtype=np.float32)[:RATE * 4]
    assert np.isfinite(pcm).all(), name + ' contains invalid audio samples'
    # Keep a recognisable call at a comfortable peak level, with short click-free fades.
    peak = max(.001, float(np.max(np.abs(pcm)))); pcm *= .7 / peak
    n = min(len(pcm) // 4, int(RATE * .035)); pcm[:n] *= np.linspace(0, 1, n)
    n = min(len(pcm) // 4, int(RATE * .1)); pcm[-n:] *= np.linspace(1, 0, n)
    target = out / (name + '.ogg')
    subprocess.run([ffmpeg, '-nostdin', '-v', 'error', '-y', '-f', 'f32le', '-ar', str(RATE), '-ac', '1', '-i', 'pipe:0', '-c:a', 'libvorbis', '-q:a', '5', str(target)], input=pcm.tobytes(), check=True)
    return hashlib.sha256(target.read_bytes()).hexdigest()

def foley(name):
    durations = {'WindGust': 1.8, 'PawRun': 1.8, 'Stampede': 2.0, 'BananaThrow': .65, 'BananaSplat': .5, 'IceSlide': 2.0, 'OceanSplash': 1.4, 'SwimBubbles': 2.0, 'CloudFlight': 2.8}
    t = np.arange(int(RATE * durations[name])) / RATE; length = t[-1]
    noise = rng.standard_normal(len(t)); soft = np.convolve(noise, np.ones(23) / 23, 'same')
    envelope = np.maximum(0, np.sin(np.pi * t / length)) ** .65
    if name in ['WindGust', 'CloudFlight']:
        y = soft * envelope * (.8 + .2 * np.sin(t * 14))
        if name == 'CloudFlight': y += .035 * (np.sin(t * 2 * np.pi * 523.25) + np.sin(t * 2 * np.pi * 783.99)) * envelope
    elif name in ['PawRun', 'Stampede']:
        y = np.zeros(len(t)); heavy = name == 'Stampede'
        for start in np.arange(0, length - .1, .27 if heavy else .19):
            u = np.maximum(0, t - start); hit = np.where(t >= start, np.exp(-u * (16 if heavy else 32)), 0)
            y += hit * (np.sin(u * 2 * np.pi * (51 if heavy else 103)) * (.7 if heavy else .35) + soft * 1.3)
    elif name == 'BananaThrow':
        y = (noise - soft) * envelope * (.35 + .65 * t / length)
    elif name == 'BananaSplat':
        y = np.exp(-t * 14) * (.6 * np.sin(2 * np.pi * (140 * t - 80 * t * t)) + soft * 2)
    elif name == 'IceSlide':
        y = (noise - np.convolve(noise, np.ones(5) / 5, 'same')) * envelope * .18
        y += .04 * np.sin(2 * np.pi * (2300 * t + 180 * t * t)) * envelope
    elif name == 'OceanSplash':
        y = soft * np.exp(-t * 2) * (.7 + .3 * np.sin(t * 32)) + noise * np.exp(-t * 8) * .12
    else:
        y = np.zeros(len(t))
        for start in np.arange(0, length - .1, .16):
            u = np.maximum(0, t - start); f = rng.uniform(450, 1500)
            y += np.sin(2 * np.pi * f * (.06 * (1 - np.exp(-u / .06)))) * np.where(t >= start, np.exp(-u * 22), 0)
    return np.tanh(y)

def main(out, source_manifest):
    out.mkdir(parents=True, exist_ok=True)
    records = json.loads(source_manifest.read_text(encoding='utf-8-sig')) if source_manifest.exists() else []
    sources = [
        ('Eagle', commons('Bald_Eagle_Yellowstone_National_Park.ogg'), 'https://commons.wikimedia.org/wiki/File:Bald_Eagle_Yellowstone_National_Park.ogg', 'National Park Service', 'Public domain / US government'),
        ('Elephant', commons('Elephant_voice_-_trumpeting.ogg'), 'https://commons.wikimedia.org/wiki/File:Elephant_voice_-_trumpeting.ogg', 'Info-farmer / தகவலுழவன்', 'CC0-1.0'),
        ('Bear', 'https://opengameart.org/sites/default/files/bear_01.ogg', 'https://opengameart.org/content/bear-growls', 'U.S. Fish & Wildlife Service; edited by AntumDeluge', 'CC0-1.0'),
        ('Lion', 'https://sfxmint.com/dl/lion-roar-02.wav', 'https://sfxmint.com/sounds/lion-roar-02', 'SFXMint', 'CC0-1.0'),
        ('Tiger', 'https://sfxmint.com/dl/short-tiger-roar-01.wav', 'https://sfxmint.com/sounds/short-tiger-roar-01', 'SFXMint', 'CC0-1.0')
    ]
    additions = []
    for name, url, page, artist, license_name in sources:
        print('Downloading ' + name, flush=True)
        request = urllib.request.Request(url, headers={'User-Agent': 'SleightGameJam/1.0 (audio assets)'})
        raw = urllib.request.urlopen(request, timeout=45).read()
        decoded = subprocess.run([ffmpeg, '-nostdin', '-v', 'error', '-i', 'pipe:0', '-ac', '1', '-ar', str(RATE), '-f', 'f32le', 'pipe:1'], input=raw, stdout=subprocess.PIPE, check=True).stdout
        samples = np.frombuffer(decoded, dtype=np.float32).copy()
        assert len(samples) > RATE // 3, name
        sha = write(out, name, samples)
        additions.append(dict(file=name + '.ogg', kind='Animal call recording' if name not in ['Lion', 'Tiger'] else 'Designed animal roar', source=page, original=url, artist=artist, license=license_name,
            license_url='https://creativecommons.org/publicdomain/zero/1.0/' if license_name == 'CC0-1.0' else 'https://www.nps.gov/aboutus/disclaimer.htm', adaptation='Trimmed to at most four seconds; mono 44.1 kHz; peak matching and short fades.', sha256=sha))
    for name in ['WindGust', 'PawRun', 'Stampede', 'BananaThrow', 'BananaSplat', 'IceSlide', 'OceanSplash', 'SwimBubbles', 'CloudFlight']:
        sha = write(out, name, foley(name))
        additions.append(dict(file=name + '.ogg', kind='Original departure Foley', source='Tools/PrepareHatJourneyAudio.py', third_party_samples=False, sha256=sha))
        print('Prepared ' + name, flush=True)
    replaced = {a['file'] for a in additions}; records = [r for r in records if r['file'] not in replaced] + additions
    (out / 'AudioSources.json').write_text(json.dumps(records, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    print('PASS upgraded calls and nine departure cues prepared.', flush=True)

if __name__ == '__main__': main(Path(sys.argv[1]), Path(sys.argv[2]) if len(sys.argv) > 2 else Path(sys.argv[1]) / 'AudioSources.json')
