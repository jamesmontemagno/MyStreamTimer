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

    func testPerTimerMissingAndUnknownPreferencesUseDefault() throws {
        try withDefaults { defaults in
            let store = LegacySettingsStore(defaults: defaults)
            let config = store.loadConfiguration(for: .countdown)
            XCTAssertEqual(config.endSound, .defaultBeep)
            XCTAssertEqual(config.soundMinutes, 5)
            XCTAssertEqual(config.soundSeconds, 0)

            defaults.set("removed-sound", forKey: "key_end_sound_countdown")
            defaults.set(99, forKey: "key_sound_seconds_countdown")
            XCTAssertEqual(LegacySettingsStore(defaults: defaults).loadConfiguration(for: .countdown).endSound, .defaultBeep)
            XCTAssertEqual(LegacySettingsStore(defaults: defaults).loadConfiguration(for: .countdown).soundSeconds, 59)
        }
    }

    func testPerTimerSoundAndCustomFilePersistWithoutChangingTimerSwitches() throws {
        try withDefaults { defaults in
            defaults.set(true, forKey: "make_sound_countdown")
            defaults.set(false, forKey: "make_sound_countdown2")
            let store = LegacySettingsStore(defaults: defaults)

            for sound in EndSound.allCases {
                var config = store.loadConfiguration(for: .countdown)
                config.endSound = sound
                store.saveConfiguration(config, for: .countdown)
                XCTAssertEqual(defaults.string(forKey: "key_end_sound_countdown"), sound.rawValue)
                XCTAssertEqual(LegacySettingsStore(defaults: defaults).loadConfiguration(for: .countdown).endSound, sound)
            }

            let bookmark = Data([1, 2, 3])
            var config = store.loadConfiguration(for: .countdown)
            config.endSound = .bell
            config.customEndSoundBookmark = bookmark
            config.customEndSoundFileName = "Finale.mp3"
            config.soundMinutes = 7
            config.soundSeconds = 42
            store.saveConfiguration(config, for: .countdown)

            let restored = LegacySettingsStore(defaults: defaults)
            let restoredConfig = restored.loadConfiguration(for: .countdown)
            XCTAssertEqual(restoredConfig.endSound, .bell)
            XCTAssertEqual(restoredConfig.customEndSoundBookmark, bookmark)
            XCTAssertEqual(restoredConfig.customEndSoundFileName, "Finale.mp3")
            XCTAssertEqual(restoredConfig.soundMinutes, 7)
            XCTAssertEqual(restoredConfig.soundSeconds, 42)
            XCTAssertTrue(restored.loadConfiguration(for: .countdown).beepAtZero)
            XCTAssertFalse(restored.loadConfiguration(for: .countdown2).beepAtZero)
            XCTAssertFalse(restored.loadConfiguration(for: .countdown3).beepAtZero)
            XCTAssertNil(defaults.object(forKey: "make_sound_countdown3"))
        }
    }

    func testPerTimerCustomSoundsAreIndependent() throws {
        try withDefaults { defaults in
            let store = LegacySettingsStore(defaults: defaults)
            store.saveCustomEndSound(bookmark: Data([1]), fileName: "One.wav", for: .countdown)
            store.saveCustomEndSound(bookmark: Data([2]), fileName: "Two.wav", for: .countdown2)

            let countdown = store.loadConfiguration(for: .countdown)
            let countdown2 = store.loadConfiguration(for: .countdown2)
            XCTAssertEqual(countdown.endSound, .custom)
            XCTAssertEqual(countdown.customEndSoundBookmark, Data([1]))
            XCTAssertEqual(countdown.customEndSoundFileName, "One.wav")
            XCTAssertEqual(countdown2.endSound, .custom)
            XCTAssertEqual(countdown2.customEndSoundBookmark, Data([2]))
            XCTAssertEqual(countdown2.customEndSoundFileName, "Two.wav")
        }
    }

    func testCountUpSoundCrossingLogic() {
        XCTAssertTrue(TimerController.shouldPlayCountUpSound(previous: 299.9, current: 300, target: 300))
        XCTAssertTrue(TimerController.shouldPlayCountUpSound(previous: 240, current: 360, target: 300))
        XCTAssertFalse(TimerController.shouldPlayCountUpSound(previous: 300, current: 301, target: 300))
        XCTAssertFalse(TimerController.shouldPlayCountUpSound(previous: 301, current: 360, target: 300))
        XCTAssertFalse(TimerController.shouldPlayCountUpSound(previous: 120, current: 240, target: 300))
        XCTAssertFalse(TimerController.shouldPlayCountUpSound(previous: 0, current: 1, target: 0))
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
        let player = EndSoundPlayer()
        for sound in EndSound.allCases where sound != .custom {
            let url = try XCTUnwrap(sound.bundledURL())
            XCTAssertEqual(url.lastPathComponent, "end-\(sound.rawValue).wav")
            let audio = try player.loadSound(sound)
            XCTAssertGreaterThan(audio.player.duration, 0)
            XCTAssertEqual(audio.player.numberOfLoops, 0)
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
        let player = EndSoundPlayer()
        for bookmark in [nil, Data([1, 2, 3])] as [Data?] {
            XCTAssertFalse(player.hasUsableCustomSound(bookmark: bookmark))
            let result = try EndSound.custom.resolve { sound in
                try player.loadSound(sound, customBookmark: bookmark)
            }
            XCTAssertTrue(result.usedFallback)
            XCTAssertGreaterThan(result.value.player.duration, 0)
        }
    }

    func testCancellationAndInvalidSelectionKeepPreviousCustomFile() throws {
        try withDefaults { defaults in
            let store = LegacySettingsStore(defaults: defaults)
            let bookmark = Data([4, 5, 6])
            var config = store.loadConfiguration(for: .countdown)
            config.endSound = .digital
            config.customEndSoundBookmark = bookmark
            config.customEndSoundFileName = "Previous.wav"
            store.saveConfiguration(config, for: .countdown)

            let player = EndSoundPlayer()
            XCTAssertNil(try player.selectCustomFile(nil))
            XCTAssertThrowsError(try player.selectCustomFile(URL(fileURLWithPath: "/sounds/wrong.txt")))
            let missingFile = try XCTUnwrap(EndSound.defaultBeep.bundledURL())
                .deletingLastPathComponent()
                .appendingPathComponent("missing-\(UUID()).mp3")
            XCTAssertThrowsError(try player.selectCustomFile(missingFile))

            let restored = LegacySettingsStore(defaults: defaults).loadConfiguration(for: .countdown)
            XCTAssertEqual(restored.endSound, .digital)
            XCTAssertEqual(restored.customEndSoundBookmark, bookmark)
            XCTAssertEqual(restored.customEndSoundFileName, "Previous.wav")
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
            let customFile = try XCTUnwrap(EndSoundPlayer().selectCustomFile(url))
            store.saveCustomEndSound(bookmark: customFile.bookmark, fileName: customFile.fileName, for: .countdown)

            let restored = LegacySettingsStore(defaults: defaults)
            let restoredConfig = restored.loadConfiguration(for: .countdown)
            XCTAssertEqual(restoredConfig.endSound, .custom)
            XCTAssertEqual(restoredConfig.customEndSoundFileName, url.lastPathComponent)
            XCTAssertNotNil(restoredConfig.customEndSoundBookmark)
            let audio = try EndSoundPlayer().loadSound(.custom, customBookmark: restoredConfig.customEndSoundBookmark)
            XCTAssertGreaterThan(audio.player.duration, 0)
            XCTAssertEqual(audio.player.numberOfLoops, 0)
        }
    }

    func testStopCancelsPlaybackAndCanBeRepeated() throws {
        let player = EndSoundPlayer()
        XCTAssertFalse(player.isPlaying)
        XCTAssertFalse(try player.play(sound: .defaultBeep, customBookmark: nil))
        XCTAssertTrue(player.isPlaying)
        player.stop()
        XCTAssertFalse(player.isPlaying)
        XCTAssertNil(player.playback)
        player.stop()
        XCTAssertFalse(player.isPlaying)
    }

    func testPlaybackCompletionClearsPlayingState() async throws {
        let player = EndSoundPlayer()
        defer { player.stop() }
        try player.play(sound: .defaultBeep, customBookmark: nil)

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
        let selectedSound = EndSound.chime
        let player = EndSoundPlayer()
        defer { player.stop() }
        try player.play(sound: selectedSound, customBookmark: nil)

        let firstFailure = expectation(description: "selected sound failed")
        player.onPlaybackError = { _ in firstFailure.fulfill() }
        let original = try XCTUnwrap(player.playback?.player)
        player.audioPlayerDidFinishPlaying(original, successfully: false)
        await fulfillment(of: [firstFailure], timeout: 5)
        let fallback = try XCTUnwrap(player.playback?.player)
        XCTAssertEqual(fallback.url?.lastPathComponent, "end-default.wav")
        XCTAssertEqual(selectedSound, .chime)

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
