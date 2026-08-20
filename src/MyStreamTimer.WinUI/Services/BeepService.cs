using System.Diagnostics;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;

namespace MyStreamTimer.WinUI.Services;

/// <summary>
/// Port of the legacy generated beep: a 2000 Hz, 75 ms, amplitude-200 tone rendered as 44.1 kHz 16-bit stereo
/// PCM WAV, played three times 200 ms apart through <see cref="MediaPlayer"/>. Never throws.
/// </summary>
public sealed class BeepService
{
    private const int Amplitude = 200;
    private const int FrequencyHz = 2000;
    private const int DurationMs = 75;
    private const int SampleRate = 44100;
    private const short Channels = 2;
    private const short BitsPerSample = 16;
    private const int Repeats = 3;
    private const int GapMs = 200;

    private readonly byte[] _wav = BuildWav();
    private MediaPlayer? _player;

    /// <summary>Plays the beep sequence. Safe to call from any thread.</summary>
    public async Task PlayAsync()
    {
        try
        {
            for (var i = 0; i < Repeats; i++)
            {
                await PlayOnceAsync().ConfigureAwait(false);
                await Task.Delay(GapMs).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BeepService] Beep failed: {ex.Message}");
        }
    }

    private async Task PlayOnceAsync()
    {
        var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(_wav);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        var player = _player ??= new MediaPlayer { AutoPlay = false };
        player.Source = MediaSource.CreateFromStream(stream, "audio/wav");
        player.Play();
    }

    private static byte[] BuildWav()
    {
        var samples = SampleRate * DurationMs / 1000;
        var blockAlign = Channels * BitsPerSample / 8;
        var dataSize = samples * blockAlign;

        using var ms = new MemoryStream(44 + dataSize);
        using var w = new BinaryWriter(ms);

        // RIFF header (44 bytes)
        w.Write("RIFF"u8);
        w.Write(36 + dataSize);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);                                   // PCM chunk size
        w.Write((short)1);                             // PCM format
        w.Write(Channels);
        w.Write(SampleRate);
        w.Write(SampleRate * blockAlign);              // byte rate
        w.Write((short)blockAlign);
        w.Write(BitsPerSample);
        w.Write("data"u8);
        w.Write(dataSize);

        var theta = FrequencyHz * Math.PI * 2 / SampleRate;
        for (var step = 0; step < samples; step++)
        {
            var sample = (short)(Amplitude * Math.Sin(theta * step));
            w.Write(sample);
            w.Write(sample);
        }

        w.Flush();
        return ms.ToArray();
    }
}
