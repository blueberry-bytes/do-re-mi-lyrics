import os
import sys
import json
import logging

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
FFMPEG_DIR = os.path.join(BASE_DIR, "ffmpeg")

DEVICE = "cpu"
COMPUTE_TYPE = "int8"
WHISPER_MODEL = "small.en"

os.add_dll_directory(FFMPEG_DIR)

logging.getLogger().setLevel(logging.ERROR)

import whisperx

sys.path.insert(0, BASE_DIR)

from whisper_common import WHISPER_SAMPLE_RATE, align_segment, fail, load_mono_audio, status, to_output_word

if len(sys.argv) != 2:
    fail("Usage: whisper-rough.py <audio-file>")

audio_path = sys.argv[1]

if not os.path.isfile(audio_path):
    fail(f"Audio file does not exist: {audio_path}")

status("Loading Whisper model...")

model = whisperx.load_model(
    WHISPER_MODEL,
    DEVICE,
    compute_type=COMPUTE_TYPE,
    language="en",
)

status("Loading audio...")

audio = load_mono_audio(audio_path)
duration = len(audio) / WHISPER_SAMPLE_RATE

status("Transcribing...")

result = model.transcribe(
    audio,
    batch_size=4,
)

rough_segments = result["segments"]

status("Loading alignment model...")

align_model, metadata = whisperx.load_align_model(
    language_code="en",
    device=DEVICE,
)

words = []

for index, segment in enumerate(rough_segments):
    status(f"Aligning words {index + 1}/{len(rough_segments)}...")

    aligned_words = align_segment(
        segment["text"],
        segment["start"],
        segment["end"],
        align_model,
        metadata,
        audio,
        DEVICE,
    )

    words += [word for word in map(to_output_word, aligned_words) if word is not None]

output = {
    "duration": duration,
    "words": words,
}

print("RESULT_JSON:" + json.dumps(
    output,
    ensure_ascii=False,
    separators=(",", ":")
))
