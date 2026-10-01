using System.Collections.Generic;

namespace Do_Re_Mi_Lyrics.Models;

public sealed class WhisperRoughResult
{
    public double Duration { get; set; }
    public List<WhisperWordDto> Words { get; set; } = [];
}