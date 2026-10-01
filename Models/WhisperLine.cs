using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Do_Re_Mi_Lyrics.Models;

public sealed class WhisperLine
{
    [JsonIgnore] public bool HasPreviousTimings => PreviousTimings.Length > 0;
    public double End { get; set; }

    [JsonIgnore] public List<LyricsLine> Lines { get; set; } = [];

    [JsonIgnore] public WordTiming[] PreviousTimings { get; set; } = [];

    public double Start { get; set; }
    public string Text { get; set; } = "";
}