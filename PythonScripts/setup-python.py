import os
import sys
import json
import tempfile
import subprocess
import urllib.request

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
FFMPEG_DIR = os.path.join(BASE_DIR, "ffmpeg")
REQUIREMENTS_PATH = os.path.join(BASE_DIR, "python-requirements.txt")
GET_PIP_URL = "https://bootstrap.pypa.io/get-pip.py"
PIP_OPTIONS = ["--no-warn-script-location", "--disable-pip-version-check", "--no-input"]


def status(message):
    print(f"STATUS:{message}", file=sys.stderr, flush=True)


def fail(message):
    print(f"ERROR: {message}", file=sys.stderr)
    sys.exit(1)


def read_requirements():
    with open(REQUIREMENTS_PATH, encoding="utf-8") as file:
        return [line.strip() for line in file if line.strip() and not line.startswith("#")]


def find_requirement(package):
    for requirement in read_requirements():
        if requirement.lower().startswith(package + "=="):
            return requirement

    fail(f"{package} is missing in {REQUIREMENTS_PATH}")


def run_pip(arguments, total_packages):
    process = subprocess.Popen(
        [sys.executable, "-m", "pip", *arguments, *PIP_OPTIONS],
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
        errors="replace",
    )

    collected = 0
    output = []

    for line in process.stdout:
        output.append(line)

        if line.startswith("Collecting "):
            collected += 1
            package = line.split()[1]
            status(f"Downloading packages {collected}/{total_packages}: {package}...")
        elif line.startswith("Installing collected packages"):
            status("Installing packages (this can take a few minutes)...")

    if process.wait() != 0:
        fail("pip failed:\n" + "".join(output[-40:]))


def install_pip():
    status("Downloading pip...")

    with tempfile.TemporaryDirectory() as directory:
        get_pip_path = os.path.join(directory, "get-pip.py")
        urllib.request.urlretrieve(GET_PIP_URL, get_pip_path)

        status("Installing pip...")
        result = subprocess.run([sys.executable, get_pip_path, *PIP_OPTIONS], capture_output=True, text=True)

        if result.returncode != 0:
            fail("get-pip.py failed:\n" + result.stdout[-2000:] + result.stderr[-2000:])


def verify_environment():
    status("Checking installation...")

    script = (
        "import os, sys; os.add_dll_directory(sys.argv[1]); "
        "import torch, torchcodec, whisperx, demucs.pretrained; "
        "from torchcodec.decoders import AudioDecoder; print('ok')"
    )
    result = subprocess.run([sys.executable, "-c", script, FFMPEG_DIR], capture_output=True, text=True)

    if result.returncode != 0 or "ok" not in result.stdout:
        fail("Installed packages cannot be loaded:\n" + result.stderr[-3000:])


if not os.path.isfile(REQUIREMENTS_PATH):
    fail(f"Requirements file does not exist: {REQUIREMENTS_PATH}")

has_pip = subprocess.run([sys.executable, "-m", "pip", "--version"], capture_output=True).returncode == 0

if not has_pip:
    install_pip()

run_pip(["install", "--no-deps", find_requirement("setuptools")], 1)
run_pip(["install", "--no-deps", "--no-build-isolation", "-r", REQUIREMENTS_PATH], len(read_requirements()))
verify_environment()

print("RESULT_JSON:" + json.dumps({"python": sys.version.split()[0]}))
