import Foundation
import XCTest
@testable import My_Stream_Timer

final class TimerCustomizationTests: XCTestCase {
    func testEffectiveAppearanceUsesDefaultsForEmptyValues() {
        XCTAssertEqual(TimerKind.countdown.effectiveTitle(displayName: ""), "Countdown 1")
        XCTAssertEqual(TimerKind.countup.effectiveTitle(displayName: " \n "), "Count Up 1")
        XCTAssertEqual(TimerKind.time.effectiveSystemImage(iconGlyph: ""), "clock")
    }

    func testEffectiveAppearanceUsesTrimmedNameAndCustomSymbol() {
        XCTAssertEqual(TimerKind.countdown2.effectiveTitle(displayName: "  Intermission  "), "Intermission")
        XCTAssertEqual(TimerKind.countdown2.effectiveSystemImage(iconGlyph: "star.fill"), "star.fill")
    }

    @MainActor
    func testAppearancePersistsUsingSharedTimerKeys() throws {
        let suiteName = "TimerCustomizationTests.\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suiteName))
        defaults.removePersistentDomain(forName: suiteName)
        defer { defaults.removePersistentDomain(forName: suiteName) }

        let store = LegacySettingsStore(defaults: defaults)
        var configuration = store.loadConfiguration(for: .countdown2)
        XCTAssertEqual(configuration.displayName, "")
        XCTAssertEqual(configuration.iconGlyph, "")

        configuration.displayName = "Intermission"
        configuration.iconGlyph = "star.fill"
        store.saveConfiguration(configuration, for: .countdown2)

        XCTAssertEqual(defaults.string(forKey: "DisplayName_countdown2"), "Intermission")
        XCTAssertEqual(defaults.string(forKey: "IconGlyph_countdown2"), "star.fill")

        let reloaded = store.loadConfiguration(for: .countdown2)
        XCTAssertEqual(reloaded.displayName, "Intermission")
        XCTAssertEqual(reloaded.iconGlyph, "star.fill")
    }
}
