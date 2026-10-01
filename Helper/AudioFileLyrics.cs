using System;
using System.IO;
using TagLib;
using File = System.IO.File;

namespace Do_Re_Mi_Lyrics.Helper;

public static class AudioFileLyrics
{
    public static string Read(string audioFilePath)
    {
        try
        {
            using TagLib.File audioFile = TagLib.File.Create(audioFilePath);
            return audioFile.Tag.Lyrics ?? "";
        }
        catch (Exception ex) when (ex is CorruptFileException or UnsupportedFormatException or IOException)
        {
            return "";
        }
    }

    public static void Write(string audioFilePath, string lyrics)
    {
        DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(audioFilePath);
        try
        {
            using TagLib.File audioFile = TagLib.File.Create(audioFilePath);
            audioFile.GetTag(audioFile is TagLib.Flac.File ? TagTypes.Xiph : TagTypes.Id3v2, true);
            audioFile.Tag.Lyrics = lyrics;
            audioFile.Save();
        }
        finally
        {
            File.SetLastWriteTimeUtc(audioFilePath, lastWriteTimeUtc);
        }
    }
}