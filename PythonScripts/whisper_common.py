import sys

import whisperx
from torchcodec.decoders import AudioDecoder

WHISPER_SAMPLE_RATE = 16000

MIN_COVERAGE = 0.5
MIN_SCAN_WINDOW = 8.0
SCAN_STEP = 1.0
SCAN_RANGE = 6.0
CLIPPED_WORD_MARGIN = 0.3


def status(message):
    print(f"STATUS:{message}", file=sys.stderr, flush=True)


def fail(message):
    print(f"ERROR: {message}", file=sys.stderr)
    sys.exit(1)


def load_mono_audio(path, sample_rate=WHISPER_SAMPLE_RATE):
    samples = AudioDecoder(path, sample_rate=sample_rate, num_channels=1).get_all_samples()

    return samples.data[0].numpy()


def to_output_word(word):
    text = word.get("word", "").strip()

    if "start" not in word or "end" not in word or not text:
        return None

    return {
        "word": text,
        "start": float(word["start"]),
        "end": float(word["end"]),
    }


def _align(text, start, end, align_model, metadata, audio, device):
    aligned = whisperx.align(
        [{"text": text, "start": start, "end": end}],
        align_model,
        metadata,
        audio,
        device,
        return_char_alignments=False,
    )

    return aligned.get("word_segments", [])


def _timed(words):
    return [word for word in words if "start" in word and "end" in word]


def _coverage(words, start, end):
    timed = _timed(words)

    if not timed or end <= start:
        return 0.0

    return (timed[-1]["end"] - timed[0]["start"]) / (end - start)


def _first_start_with_spread_words(text, start, end, align_model, metadata, audio, device):
    scan_start = start + SCAN_STEP

    while scan_start < end - MIN_SCAN_WINDOW / 2:
        attempt = _align(text, scan_start, end, align_model, metadata, audio, device)

        if _coverage(attempt, scan_start, end) >= MIN_COVERAGE:
            return scan_start

        scan_start += SCAN_STEP

    return None


def _median_unclipped_positions(words, attempts):
    result = []

    for index, word in enumerate(attempts[0][1]):
        positions = [
            attempt[index]
            for window_start, attempt in attempts
            if "start" in attempt[index] and attempt[index]["start"] - window_start >= CLIPPED_WORD_MARGIN
        ]

        if not positions:
            positions = [attempt[index] for _, attempt in attempts if "start" in attempt[index]]

        if not positions:
            result.append(word)
            continue

        positions.sort(key=lambda x: x["start"])
        result.append(positions[len(positions) // 2])

    return result


def align_segment(text, start, end, align_model, metadata, audio, device):
    words = _align(text, start, end, align_model, metadata, audio, device)

    if end - start < MIN_SCAN_WINDOW or _coverage(words, start, end) >= MIN_COVERAGE:
        return words

    first_good = _first_start_with_spread_words(text, start, end, align_model, metadata, audio, device)

    if first_good is None:
        return words

    attempts = []
    scan_start = first_good

    while scan_start <= first_good + SCAN_RANGE and scan_start < end - MIN_SCAN_WINDOW / 2:
        attempt = _align(text, scan_start, end, align_model, metadata, audio, device)

        if len(attempt) == len(words):
            attempts.append((scan_start, attempt))

        scan_start += SCAN_STEP

    if not attempts:
        return words

    return _median_unclipped_positions(words, attempts)
