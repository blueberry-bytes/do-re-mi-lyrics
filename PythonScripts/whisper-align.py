import os
import sys
import json
import logging

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
FFMPEG_DIR = os.path.join(BASE_DIR, "ffmpeg")

DEVICE = "cpu"

os.add_dll_directory(FFMPEG_DIR)

logging.getLogger().setLevel(logging.ERROR)

import whisperx

sys.path.insert(0, BASE_DIR)

from whisper_common import align_segment, fail, load_mono_audio, status, to_output_word

if len(sys.argv) != 2:
    fail("Usage: whisper-align.py <audio-file>")

audio_path = sys.argv[1]

if not os.path.isfile(audio_path):
    fail(f"Audio file does not exist: {audio_path}")

try:
    input_text = sys.stdin.read()

    if not input_text.strip():
        fail("No input lines received on stdin")

    input_lines = json.loads(input_text)

except json.JSONDecodeError as ex:
    fail(f"Invalid input JSON: {ex}")

if not isinstance(input_lines, list):
    fail("Input JSON must be an array")

segments = []

for index, line in enumerate(input_lines):

    if not isinstance(line, dict):
        fail(f"Line {index} is not an object")

    text = line.get("text")
    start = line.get("start")
    end = line.get("end")

    if not isinstance(text, str) or not text.strip():
        fail(f"Line {index}: missing or empty 'text'")

    if start is None or end is None:
        fail(f"Line {index}: missing 'start' or 'end'")

    try:
        start = float(start)
        end = float(end)
    except (TypeError, ValueError):
        fail(f"Line {index}: invalid start/end")

    if end <= start:
        fail(f"Line {index}: end must be greater than start")

    segments.append({
        "text": text,
        "start": start,
        "end": end,
    })

status("Loading alignment model...")

align_model, metadata = whisperx.load_align_model(
    language_code="en",
    device=DEVICE,
)

status("Loading audio...")

audio = load_mono_audio(audio_path)

words = []

for index, segment in enumerate(segments):
    status(f"Aligning {index + 1}/{len(segments)}...")

    aligned_words = align_segment(
        segment["text"],
        segment["start"],
        segment["end"],
        align_model,
        metadata,
        audio,
        DEVICE,
    )

    for word in map(to_output_word, aligned_words):
        if word is not None:
            word["line"] = index
            words.append(word)

output = {
    "words": words,
}

print("RESULT_JSON:" + json.dumps(
    output,
    ensure_ascii=False,
    separators=(",", ":")
))
