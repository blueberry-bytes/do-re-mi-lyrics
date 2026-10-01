namespace Do_Re_Mi_Lyrics.Models;

public sealed class WhisperWordDto
{
    public double End { get; set; }
    public int? Line { get; set; }
    public double Start { get; set; }
    public string Word { get; set; } = "";
}