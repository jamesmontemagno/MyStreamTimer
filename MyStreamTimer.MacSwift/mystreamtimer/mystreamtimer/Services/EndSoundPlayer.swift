import AppKit
import AVFoundation
import Combine
import Foundation
import UniformTypeIdentifiers

enum EndSoundError: LocalizedError {
    case unsupportedFile
    case noCustomFile
    case invalidAudio
    case missingBundledSound
    case playbackFailed

    var errorDescription: String? {
        switch self {
        case .unsupportedFile:
            "Choose an MP3 or WAV (.wav or .wave) audio file."
        case .noCustomFile:
            "Choose a custom MP3 or WAV sound first."
        case .invalidAudio:
            "This file could not be opened or decoded as audio. Choose a readable, valid MP3 or WAV file."
        case .missingBundledSound:
            "The bundled end sound could not be found."
        case .playbackFailed:
            "The sound could not be played. Check your audio output and try again."
        }
    }
}

struct CustomEndSoundFile {
    let bookmark: Data
    let fileName: String
}

@MainActor
final class EndSoundPlayback {
    let player: AVAudioPlayer
    private let scopedURL: URL?

    init(url: URL, securityScoped: Bool) throws {
        let hasAccess = securityScoped && url.startAccessingSecurityScopedResource()
        do {
            self.player = try EndSoundPlayer.makePlayer(url: url)
            self.scopedURL = hasAccess ? url : nil
        } catch {
            if hasAccess { url.stopAccessingSecurityScopedResource() }
            throw error
        }
    }

    deinit {
        scopedURL?.stopAccessingSecurityScopedResource()
    }
}

@MainActor
final class EndSoundPlayer: NSObject, ObservableObject, AVAudioPlayerDelegate {
    @Published private(set) var isPlaying = false
    var onPlaybackError: ((String) -> Void)?

    private(set) var playback: EndSoundPlayback?
    private var playingDefault = false

    func hasUsableCustomSound(bookmark: Data?) -> Bool {
        (try? loadSound(.custom, customBookmark: bookmark)) != nil
    }

    func chooseCustomSound() throws -> CustomEndSoundFile? {
        let panel = NSOpenPanel()
        panel.canChooseDirectories = false
        panel.canChooseFiles = true
        panel.allowsMultipleSelection = false
        panel.allowedContentTypes = EndSound.supportedExtensions.compactMap {
            UTType(filenameExtension: $0)
        }
        panel.title = "Choose a custom end sound"
        panel.message = "Choose an MP3 or WAV file. The entire track plays once."
        panel.prompt = "Choose Sound"
        guard panel.runModal() == .OK else { return nil }
        return try selectCustomFile(panel.url)
    }

    func selectCustomFile(_ url: URL?) throws -> CustomEndSoundFile? {
        guard let url else { return nil }
        guard EndSound.supports(url) else { throw EndSoundError.unsupportedFile }

        let hasAccess = url.startAccessingSecurityScopedResource()
        defer {
            if hasAccess { url.stopAccessingSecurityScopedResource() }
        }
        _ = try Self.makePlayer(url: url)
        let bookmark = try url.bookmarkData(
            options: [.withSecurityScope, .securityScopeAllowOnlyReadAccess],
            includingResourceValuesForKeys: nil,
            relativeTo: nil
        )
        return CustomEndSoundFile(bookmark: bookmark, fileName: url.lastPathComponent)
    }

    @discardableResult
    func play(
        sound: EndSound,
        customBookmark: Data?,
        updateCustomBookmark: ((Data) -> Void)? = nil
    ) throws -> Bool {
        stop()
        let result = try sound.resolve { selectedSound in
            try startPlayer(
                loadSound(
                    selectedSound,
                    customBookmark: customBookmark,
                    updateCustomBookmark: updateCustomBookmark
                ),
                sound: selectedSound
            )
        }
        return result.usedFallback
    }

    func stop() {
        playback?.player.delegate = nil
        playback?.player.stop()
        playback = nil
        isPlaying = false
    }

    func playAtCompletion(
        sound: EndSound,
        customBookmark: Data?,
        updateCustomBookmark: ((Data) -> Void)? = nil
    ) {
        do {
            try play(sound: sound, customBookmark: customBookmark, updateCustomBookmark: updateCustomBookmark)
        } catch {
            NSSound.beep()
        }
    }

    private func startPlayer(_ candidate: EndSoundPlayback, sound: EndSound) throws {
        playback = candidate
        playingDefault = sound == .defaultBeep
        candidate.player.delegate = self
        guard candidate.player.play() else {
            stop()
            throw EndSoundError.playbackFailed
        }
        isPlaying = true
    }

    nonisolated func audioPlayerDidFinishPlaying(_ player: AVAudioPlayer, successfully flag: Bool) {
        let identity = ObjectIdentifier(player)
        Task { @MainActor [weak self] in
            self?.finishPlayback(identity: identity, successfully: flag)
        }
    }

    nonisolated func audioPlayerDecodeErrorDidOccur(_ player: AVAudioPlayer, error: Error?) {
        let identity = ObjectIdentifier(player)
        Task { @MainActor [weak self] in
            self?.finishPlayback(identity: identity, successfully: false)
        }
    }

    private func finishPlayback(identity: ObjectIdentifier, successfully: Bool) {
        guard let playback, ObjectIdentifier(playback.player) == identity else { return }
        let wasDefault = playingDefault
        stop()
        guard !successfully else { return }

        var message = "The end sound could not finish playing."
        if !wasDefault {
            do {
                try startPlayer(loadSound(.defaultBeep, customBookmark: nil), sound: .defaultBeep)
                message += " Default beep is playing instead."
            } catch {
                NSSound.beep()
            }
        } else {
            NSSound.beep()
        }
        onPlaybackError?(message)
    }

    func loadSound(
        _ sound: EndSound,
        customBookmark: Data? = nil,
        updateCustomBookmark: ((Data) -> Void)? = nil
    ) throws -> EndSoundPlayback {
        guard sound == .custom else {
            guard let url = sound.bundledURL() else { throw EndSoundError.missingBundledSound }
            return try EndSoundPlayback(url: url, securityScoped: false)
        }

        guard let customBookmark else {
            throw EndSoundError.noCustomFile
        }
        var isStale = false
        let url = try URL(
            resolvingBookmarkData: customBookmark,
            options: [.withSecurityScope, .withoutUI],
            relativeTo: nil,
            bookmarkDataIsStale: &isStale
        )
        guard EndSound.supports(url) else { throw EndSoundError.unsupportedFile }
        let candidate = try EndSoundPlayback(url: url, securityScoped: true)
        if isStale {
            let refreshedBookmark = try url.bookmarkData(
                options: [.withSecurityScope, .securityScopeAllowOnlyReadAccess],
                includingResourceValuesForKeys: nil,
                relativeTo: nil
            )
            updateCustomBookmark?(refreshedBookmark)
        }
        return candidate
    }

    static func makePlayer(url: URL) throws -> AVAudioPlayer {
        do {
            return try preparePlayer(AVAudioPlayer(contentsOf: url))
        } catch {
            throw EndSoundError.invalidAudio
        }
    }

    static func preparePlayer(_ player: AVAudioPlayer) throws -> AVAudioPlayer {
        guard player.duration.isFinite, player.duration > 0,
              player.prepareToPlay() else {
            throw EndSoundError.invalidAudio
        }
        player.numberOfLoops = 0
        return player
    }
}
