import AVFoundation
import Combine
import Foundation
import XCTest
@testable import My_Stream_Timer

@MainActor
final class EndSoundTests: XCTestCase {
    func testCatalogHasStableIDsAndLabels() {
        XCTAssertEqual(EndSound.allCases.map(\.rawValue), ["default", "chime", "bell", "digital", "custom"])
        XCTAssertEqual(EndSound.allCases.map(\.displayName), ["Default beep", "Chime", "Bell", "Digital", "Custom"])
        XCTAssertNil(EndSound.custom.bundledURL())
    }

    func testMissingAndUnknownPreferencesUseDefault() throws {
        try withDefaults { defaults in
            XCTAssertEqual(LegacySettingsStore(defaults: defaults).endSound, .defaultBeep)
            defaults.set("removed-sound", forKey: "EndSound")
            XCTAssertEqual(LegacySettingsStore(defaults: defaults).endSound, .defaultBeep)
        }
    }

    func testGlobalSoundAndCustomFilePersistWithoutChangingTimerSwitches() throws {
        try withDefaults { defaults in
            defaults.set(true, forKey: "make_sound_countdown")
            defaults.set(false, forKey: "make_sound_countdown2")
            let store = LegacySettingsStore(defaults: defaults)
            for sound in EndSound.allCases {
                store.endSound = sound
                XCTAssertEqual(defaults.string(forKey: "EndSound"), sound.rawValue)
                XCTAssertEqual(LegacySettingsStore(defaults: defaults).endSound, sound)
            }

            let bookmark = Data([1, 2, 3])
            store.saveCustomEndSound(bookmark: bookmark, fileName: "Finale.mp3")
            store.endSound = .bell
            let restored = LegacySettingsStore(defaults: defaults)
            XCTAssertEqual(restored.endSound, .bell)
            XCTAssertEqual(restored.customEndSoundBookmark, bookmark)
            XCTAssertEqual(restored.customEndSoundFileName, "Finale.mp3")
            XCTAssertTrue(restored.loadConfiguration(for: .countdown).beepAtZero)
            XCTAssertFalse(restored.loadConfiguration(for: .countdown2).beepAtZero)
            XCTAssertFalse(restored.loadConfiguration(for: .countdown3).beepAtZero)
            XCTAssertNil(defaults.object(forKey: "make_sound_countdown3"))
        }
    }

    func testSupportedAudioExtensionsAreCaseInsensitive() {
        for name in ["track.mp3", "track.MP3", "track.wav", "track.WAV", "track.wave", "track.WaVe"] {
            XCTAssertTrue(EndSound.supports(URL(fileURLWithPath: "/sounds/\(name)")), name)
        }
        for name in ["track", "track.txt", "track.aiff", "track.mp3.exe", "track.m4a"] {
            XCTAssertFalse(EndSound.supports(URL(fileURLWithPath: "/sounds/\(name)")), name)
        }
        XCTAssertFalse(EndSound.supports(URL(string: "https://example.com/track.mp3")!))
    }

    func testEveryBundledSoundCanBeDecodedAndPlaysOnce() throws {
        try withDefaults { defaults in
            let player = EndSoundPlayer(settingsStore: LegacySettingsStore(defaults: defaults))
            for sound in EndSound.allCases where sound != .custom {
                let url = try XCTUnwrap(sound.bundledURL())
                XCTAssertEqual(url.lastPathComponent, "end-\(sound.rawValue).wav")
                let audio = try player.loadSound(sound)
                XCTAssertGreaterThan(audio.player.duration, 0)
                XCTAssertEqual(audio.player.numberOfLoops, 0)
            }
        }
    }

    func testFallbackLoadsDefaultForAnyUnavailableChoice() throws {
        for errorCode in [CocoaError.Code.fileReadNoSuchFile, .fileReadNoPermission, .fileReadCorruptFile] {
            for selected in EndSound.allCases where selected != .defaultBeep {
                var attempted: [EndSound] = []
                let result = try selected.resolve { sound in
                    attempted.append(sound)
                    if sound == selected { throw CocoaError(errorCode) }
                    return sound
                }
                XCTAssertEqual(result.value, .defaultBeep)
                XCTAssertTrue(result.usedFallback)
                XCTAssertEqual(attempted, [selected, .defaultBeep])
            }
        }
    }

    func testAvailableSoundDoesNotFallBackAndDefaultFailureDoesNotRetry() throws {
        let result = try EndSound.chime.resolve { $0 }
        XCTAssertEqual(result.value, .chime)
        XCTAssertFalse(result.usedFallback)

        var attempts = 0
        XCTAssertThrowsError(try EndSound.defaultBeep.resolve { _ -> EndSound in
            attempts += 1
            throw CocoaError(.fileReadNoSuchFile)
        })
        XCTAssertEqual(attempts, 1)
    }

    func testMissingAndInvalidBookmarksFallBackWithoutErasingPreference() throws {
        try withDefaults { defaults in
            let store = LegacySettingsStore(defaults: defaults)
            let player = EndSoundPlayer(settingsStore: store)
            store.endSound = .custom
            for bookmark in [nil, Data([1, 2, 3])] as [Data?] {
                store.customEndSoundBookmark = bookmark
                XCTAssertFalse(player.hasUsableCustomSound)
                let result = try store.endSound.resolve { try player.loadSound($0) }
                XCTAssertTrue(result.usedFallback)
                XCTAssertGreaterThan(result.value.player.duration, 0)
                XCTAssertEqual(store.endSound, .custom)
                XCTAssertEqual(store.customEndSoundBookmark, bookmark)
            }
        }
    }

    func testCancellationAndInvalidSelectionKeepPreviousCustomFile() throws {
        try withDefaults { defaults in
            let store = LegacySettingsStore(defaults: defaults)
            let bookmark = Data([4, 5, 6])
            store.saveCustomEndSound(bookmark: bookmark, fileName: "Previous.wav")
            store.endSound = .digital
            let player = EndSoundPlayer(settingsStore: store)
            try player.selectCustomFile(nil)
            XCTAssertThrowsError(try player.selectCustomFile(URL(fileURLWithPath: "/sounds/wrong.txt")))
            let missingFile = try XCTUnwrap(EndSound.defaultBeep.bundledURL())
                .deletingLastPathComponent()
                .appendingPathComponent("missing-\(UUID()).mp3")
            XCTAssertThrowsError(try player.selectCustomFile(missingFile))
            XCTAssertEqual(store.endSound, .digital)
            XCTAssertEqual(store.customEndSoundBookmark, bookmark)
            XCTAssertEqual(store.customEndSoundFileName, "Previous.wav")
        }
    }

    func testUndecodableAudioIsRejected() {
        XCTAssertThrowsError(try EndSoundPlayer.preparePlayer(AVAudioPlayer(data: Data())))
        XCTAssertThrowsError(try EndSoundPlayer.preparePlayer(AVAudioPlayer(data: Data("not audio".utf8))))
    }

    func testSelectedWAVBookmarkLoadsAfterSettingsRecreation() throws {
        try withDefaults { defaults in
            let store = LegacySettingsStore(defaults: defaults)
            let url = try XCTUnwrap(EndSound.bell.bundledURL())
            try EndSoundPlayer(settingsStore: store).selectCustomFile(url)

            let restored = LegacySettingsStore(defaults: defaults)
            XCTAssertEqual(restored.endSound, .custom)
            XCTAssertEqual(restored.customEndSoundFileName, url.lastPathComponent)
            XCTAssertNotNil(restored.customEndSoundBookmark)
            let audio = try EndSoundPlayer(settingsStore: restored).loadSound(.custom)
            XCTAssertGreaterThan(audio.player.duration, 0)
            XCTAssertEqual(audio.player.numberOfLoops, 0)
        }
    }

    func testStopCancelsPlaybackAndCanBeRepeated() throws {
        try withDefaults { defaults in
            let player = EndSoundPlayer(settingsStore: LegacySettingsStore(defaults: defaults))
            XCTAssertFalse(player.isPlaying)
            XCTAssertFalse(try player.play())
            XCTAssertTrue(player.isPlaying)
            player.stop()
            XCTAssertFalse(player.isPlaying)
            XCTAssertNil(player.playback)
            player.stop()
            XCTAssertFalse(player.isPlaying)
        }
    }

    func testPlaybackCompletionClearsPlayingState() async throws {
        let suiteName = "EndSoundTests.\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suiteName))
        defer { defaults.removePersistentDomain(forName: suiteName) }
        let player = EndSoundPlayer(settingsStore: LegacySettingsStore(defaults: defaults))
        defer { player.stop() }
        try player.play()

        let finished = expectation(description: "end sound finished")
        let subscription = player.$isPlaying
            .dropFirst()
            .filter { !$0 }
            .sink { _ in finished.fulfill() }
        await fulfillment(of: [finished], timeout: 5)
        withExtendedLifetime(subscription) {}
        XCTAssertFalse(player.isPlaying)
        XCTAssertNil(player.playback)
    }

    func testPlaybackAndDecodeFailuresFallBackOnlyOnce() async throws {
        let suiteName = "EndSoundTests.\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suiteName))
        defer { defaults.removePersistentDomain(forName: suiteName) }
        let store = LegacySettingsStore(defaults: defaults)
        store.endSound = .chime
        let player = EndSoundPlayer(settingsStore: store)
        defer { player.stop() }
        try player.play()

        let firstFailure = expectation(description: "selected sound failed")
        player.onPlaybackError = { _ in firstFailure.fulfill() }
        let original = try XCTUnwrap(player.playback?.player)
        player.audioPlayerDidFinishPlaying(original, successfully: false)
        await fulfillment(of: [firstFailure], timeout: 5)
        let fallback = try XCTUnwrap(player.playback?.player)
        XCTAssertEqual(fallback.url?.lastPathComponent, "end-default.wav")
        XCTAssertEqual(store.endSound, .chime)

        let fallbackFailure = expectation(description: "fallback failed without retry")
        player.onPlaybackError = { _ in fallbackFailure.fulfill() }
        player.audioPlayerDecodeErrorDidOccur(fallback, error: nil)
        await fulfillment(of: [fallbackFailure], timeout: 5)
        XCTAssertFalse(player.isPlaying)
        XCTAssertNil(player.playback)
    }

    private func withDefaults(_ body: (UserDefaults) throws -> Void) throws {
        let suiteName = "EndSoundTests.\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suiteName))
        defaults.removePersistentDomain(forName: suiteName)
        defer { defaults.removePersistentDomain(forName: suiteName) }
        try body(defaults)
    }
}
