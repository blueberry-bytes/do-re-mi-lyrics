using Do_Re_Mi_Lyrics.Models;

namespace Do_Re_Mi_Lyrics.ViewModels;

public class NoLyricsInAudioWindowViewModel
{
    public NoLyricsInAudioChoice Choice { get; private set; } = NoLyricsInAudioChoice.Keep;

    public void Clear()
    {
        Choice = NoLyricsInAudioChoice.Clear;
    }

    public void Open()
    {
        Choice = NoLyricsInAudioChoice.Open;
    }

    public void Keep()
    {
        Choice = NoLyricsInAudioChoice.Keep;
    }
}