[![download]][Latest] [![l]](LICENSE)

Do-Re-Mi Lyrics is free, open-source software for
creating [enhanced lyrics](https://en.wikipedia.org/wiki/LRC_(file_format)#Enhanced_format). It plays FLAC, MP3 and
WAV files and it's easy to use. You can use it with karaoke software or with lyrics software,
e.g. [MiniLyrics](https://www.crintsoft.com).

Download: [latest version][Latest]

How to use:

* open audio file
* open a lyrics’ file or paste it from the clipboard
* start audio (Space or button)
* use F6 to set time at the start of every word
* use F5 to set ending time for the previous word (if there's a longer gap between two words/lines)
* use F8 to delete last time
* use arrow keys and mouse to change the current word
* red time means that it's earlier than the previous one and it should be corrected

Lyrics are always converted to lowercase. Text pasted from the clipboard is also cleaned up: punctuation and other special
characters, text in square brackets (e.g. [Chorus]) and empty lines are removed and numbers are written as words (1990 → nineteen ninety). Timestamps of already synchronized lyrics are kept.

How to split a word into parts? Sometimes one syllable in a word is being sung several times longer than the rest. Then
the word can be divided into two or more parts.

* enter edit mode (F12)
* write vertical bar character | in the place where you want to split
* leave the edit mode (F12)
* now you can set the time for every part of the word separately.

Automatic synchronization (F11):

* open the audio file and the lyrics, then press F11
* the program separates the vocals from the music, recognizes the sung words and sets the times of your lyrics' words;
  the text of your lyrics is never changed
* it works for English songs and takes about a minute and a half for a song (on CPU)
* on the first use it downloads its components (about 3 GB, an internet connection is needed), which takes a few more
  minutes; they are stored in `%LOCALAPPDATA%\Do-Re-Mi Lyrics` and removed by the uninstaller
* Esc cancels the synchronization, Ctrl+Z restores the lyrics from before it
* lines that may be inaccurate are highlighted in light orange, the mark disappears after changing a time in the line
* Ctrl+F11 (or the synchronization button with Ctrl held) synchronizes only the line with the highlighted word, between
  its start and the start of the next line (or the end of its last word, if it has one), 0.25 s wider on both sides;
  correct the line edges first (F6 on its first word and on the first word of the next line) and let the program set
  the words inside; Ctrl+Z restores the line from before it

Errors are written with details to `%LOCALAPPDATA%\Do-Re-Mi Lyrics\Logs\errors.log` (attach it when reporting a bug);
the folder can be opened with the Open log folder button in the About window (F1).

![Do-Re-Mi Lyrics](https://user-images.githubusercontent.com/5322956/148644781-66c0b717-6c07-4ab4-b3a2-2b88bb669296.png)

Mouse shortcuts:

* Left Mouse Button – Highlight word
* Double Left Mouse Button – Change playing time to the current word minus 1.5 seconds (at the current tempo)

Keyboard shortcuts:

* F1 - About/Help/Changelog/License
* F2 – New lyrics
* F3 – Open audio file
* F4 – Open lyrics file
* F5 – Set time at the end of the previous word (if there's a longer gap between two words/lines)
* F6 – Set time at the beginning of the highlighted word
* F8 – Removes time from the previous word
* F11 – Automatic synchronization
* Ctrl+F11 – Automatic synchronization of the current line only
* F12 – Edit text of the lyrics
* Esc – Cancel the automatic synchronization
* Ctrl+S - Save lyrics’ file
* Ctrl+Shift+S – Save lyrics to a new file
* Ctrl+Shift+A – Save lyrics inside the audio file (its modification date stays unchanged)
* Ctrl+V – Paste lyrics’ text from the clipboard
* Ctrl+Y, Ctrl+Shift+Z - Redo
* Ctrl+Z - Undo
* Left – Highlight the previous word
* Right – Highlight the next word
* Up – Highlight the first word in the previous line
* Down – Highlight the first word in the next line
* Ctrl+Minus – Decrease time of all words starting from current by 0.2 seconds
* Ctrl+Plus – Increase time of all words starting from current by 0.2 seconds
* Minus – Decrease time of the current word by 0.2 seconds
* Plus – Increase time of the current word by 0.2 seconds
* Ctrl+Enter – Change playing time to the current word minus 1.5 seconds (at the current tempo)
* Enter – Move the current word and the rest of the line to a new line
* Backspace - Move the current line to the previous one (works only when the first word in the line is highlighted)
* Delete - Move the next line to the end of the current one (works only when the last word in the line is highlighted)
* Space - Play/Pause
* Ctrl+Left – Rewind 1 second (at the current tempo)
* Ctrl+Right – Fast forward 1 second (at the current tempo)
* Ctrl+Up – Increase tempo by 0.1
* Ctrl+Down – Decrease tempo by 0.1
* Shift+Up – Increase volume by 10%
* Shift+Down – Decrease volume by 10%

Third-party components:

* audio playback: [NAudio](https://github.com/naudio/NAudio),
  [SoundTouch.Net](https://github.com/owoudenberg/soundtouch.net) (LGPL 2.1 or later)
* automatic synchronization: [Python](https://www.python.org)
  (included), [WhisperX](https://github.com/m-bain/whisperX),
  [faster-whisper](https://github.com/SYSTRAN/faster-whisper), [Demucs](https://github.com/facebookresearch/demucs),
  [PyTorch](https://pytorch.org) (downloaded on the first use), each under its own license
* [FFmpeg](https://ffmpeg.org) 7.1.1 libraries (GPL 3), build from [gyan.dev](https://www.gyan.dev/ffmpeg/builds/),
  where the source code is available

[Latest]: https://github.com/Woo-Cash/do-re-mi-lyrics/releases/latest "GitHub latest stable downloads"
[download]: https://img.shields.io/github/v/release/woo-cash/do-re-mi-lyrics?label=download
[l]: https://img.shields.io/badge/license-GPL3-blue.svg
