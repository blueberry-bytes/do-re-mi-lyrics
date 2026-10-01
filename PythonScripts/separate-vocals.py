import os
import sys
import json
import logging

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
FFMPEG_DIR = os.path.join(BASE_DIR, "ffmpeg")

SEPARATION_MODEL = "htdemucs"
SEGMENT_OVERLAP = 0.25

os.add_dll_directory(FFMPEG_DIR)

logging.getLogger().setLevel(logging.ERROR)

import numpy as np
import torch
from scipy.io import wavfile
from demucs.apply import apply_model
from demucs.pretrained import get_model

sys.path.insert(0, BASE_DIR)

from whisper_common import fail, load_mono_audio, status

if len(sys.argv) not in (3, 5):
    fail("Usage: separate-vocals.py <audio-file> <vocals-wav-file> [<start-seconds> <end-seconds>]")

audio_path = sys.argv[1]
vocals_path = sys.argv[2]
cut_range = None

if len(sys.argv) == 5:
    try:
        cut_range = (float(sys.argv[3]), float(sys.argv[4]))
    except ValueError:
        fail("Start and end must be numbers")

    if cut_range[1] <= cut_range[0]:
        fail("End must be greater than start")

if not os.path.isfile(audio_path):
    fail(f"Audio file does not exist: {audio_path}")

status("Loading separation model...")

model = get_model(SEPARATION_MODEL)
model.eval()

status("Loading audio...")

mono = load_mono_audio(audio_path, model.samplerate)

if cut_range is not None:
    mono = mono[int(cut_range[0] * model.samplerate):int(cut_range[1] * model.samplerate)]

    if len(mono) == 0:
        fail("The selected range is outside the audio")

mix = torch.from_numpy(np.stack([mono, mono])).float()
reference = mix.mean(0)
mix = (mix - reference.mean()) / reference.std()

status("Separating vocals...")

torch.set_num_threads(os.cpu_count())

with torch.no_grad():
    sources = apply_model(model, mix[None], device="cpu", shifts=0, split=True, overlap=SEGMENT_OVERLAP,
                          progress=False)[0]

sources = sources * reference.std() + reference.mean()
vocals = sources[model.sources.index("vocals")].mean(0).numpy()
peak = max(1e-9, float(np.abs(vocals).max()))

wavfile.write(vocals_path, model.samplerate, (vocals / peak * 0.95 * 32767).astype(np.int16))

print("RESULT_JSON:" + json.dumps({"path": vocals_path}, ensure_ascii=False))
