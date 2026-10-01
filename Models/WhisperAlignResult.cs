using System.Collections.Generic;

namespace Do_Re_Mi_Lyrics.Models;

public sealed class WhisperAlignResult
{
    public List<WhisperWordDto> Words { get; set; } = [];
}