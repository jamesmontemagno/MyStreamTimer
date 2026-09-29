using System.Diagnostics;
using MyStreamTimer.Core.Settings;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace MyStreamTimer.WinUI.Services;

public enum EndSoundPlaybackResult
{
    Played,
    DefaultFallback,
    Unavailable,
    Cancelled,
}

/// <summary>Plays complete end sounds once, serializing timer completions and previews. Never throws to callers.</summary>
public sealed class BeepService
{
    private static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(15);
    private readonly GlobalSettings _settings;
    private readonly SemaphoreSlim _playbackGate = new(1, 1);

    public BeepService(GlobalSettings settings) => _settings = settings;

    public async Task PlayAsync() =>
        await Task.Run(() => PlaySelectedAsync(CancellationToken.None)).ConfigureAwait(false);

    public Task<EndSoundPlaybackResult> PreviewAsync(CancellationToken cancellationToken) =>
        Task.Run(() => PlaySelectedAsync(cancellationToken));

    /// <summary>Opens the file with the same decoder used for playback, without making a sound.</summary>
    public static async Task<bool> ValidateCustomFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return EndSounds.IsSupportedCustomFile(path)
                && await Task.Run(() => TryPlayFileAsync(path, validateOnly: true, cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BeepService] Audio validation failed: {ex.Message}");
            return false;
        }
    }

    private async Task<EndSoundPlaybackResult> PlaySelectedAsync(CancellationToken cancellationToken)
    {
        var acquired = false;
        try
        {
            await _playbackGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;

            var id = _settings.EndSound;
            var path = id == EndSounds.Custom ? _settings.CustomEndSoundPath : BuiltInPath(id);
            if ((id != EndSounds.Custom || EndSounds.IsSupportedCustomFile(path))
                && await TryPlayFileAsync(path, validateOnly: false, cancellationToken).ConfigureAwait(false))
            {
                return EndSoundPlaybackResult.Played;
            }

            if (id != EndSounds.Default
                && await TryPlayFileAsync(BuiltInPath(EndSounds.Default), validateOnly: false, cancellationToken).ConfigureAwait(false))
            {
                return EndSoundPlaybackResult.DefaultFallback;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return EndSoundPlaybackResult.Cancelled;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BeepService] End sound failed: {ex.Message}");
        }
        finally
        {
            if (acquired)
            {
                _playbackGate.Release();
            }
        }

        return EndSoundPlaybackResult.Unavailable;
    }

    private static string BuiltInPath(string id) =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", EndSounds.GetBuiltInFileName(id));

    private static async Task<bool> TryPlayFileAsync(string path, bool validateOnly, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Path.IsPathFullyQualified(path) || !File.Exists(path))
            {
                return false;
            }

            using var source = MediaSource.CreateFromUri(new Uri(path));
            var item = new MediaPlaybackItem(source);
            using var player = new MediaPlayer { AutoPlay = false, IsLoopingEnabled = false, IsMuted = validateOnly };
            player.CommandManager.IsEnabled = false;
            var opened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var ended = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnOpened(MediaPlayer sender, object args) => opened.TrySetResult(true);
            void OnEnded(MediaPlayer sender, object args) => ended.TrySetResult(true);
            void OnFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
            {
                opened.TrySetResult(false);
                ended.TrySetResult(false);
            }

            player.MediaOpened += OnOpened;
            player.MediaEnded += OnEnded;
            player.MediaFailed += OnFailed;
            try
            {
                player.Source = item;
                if (!await opened.Task.WaitAsync(OpenTimeout, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }

                var duration = player.PlaybackSession.NaturalDuration;
                if (duration <= TimeSpan.Zero || item.AudioTracks.Count == 0 || ended.Task.IsCompleted)
                {
                    return false;
                }

                if (validateOnly)
                {
                    return true;
                }

                cancellationToken.ThrowIfCancellationRequested();
                player.Play();
                return await ended.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                player.MediaOpened -= OnOpened;
                player.MediaEnded -= OnEnded;
                player.MediaFailed -= OnFailed;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BeepService] Couldn't open or play audio: {ex.Message}");
            return false;
        }
    }
}
