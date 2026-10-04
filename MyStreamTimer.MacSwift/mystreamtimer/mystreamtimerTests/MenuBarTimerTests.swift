import Foundation
import XCTest
@testable import My_Stream_Timer

@MainActor
final class MenuBarTimerTests: XCTestCase {
    func testIntervalUsesMinutesAndSecondsBelowAnHour() {
        XCTAssertEqual(MenuBarTimeFormatter.string(for: 0), "0:00")
        XCTAssertEqual(MenuBarTimeFormatter.string(for: 9), "0:09")
        XCTAssertEqual(MenuBarTimeFormatter.string(for: 59), "0:59")
        XCTAssertEqual(MenuBarTimeFormatter.string(for: 60), "1:00")
        XCTAssertEqual(MenuBarTimeFormatter.string(for: 299), "4:59")
        XCTAssertEqual(MenuBarTimeFormatter.string(for: 3_599), "59:59")
    }

    func testIntervalAddsHoursFromOneHour() {
        XCTAssertEqual(MenuBarTimeFormatter.string(for: 3_600), "1:00:00")
        XCTAssertEqual(MenuBarTimeFormatter.string(for: 3_899), "1:04:59")
        XCTAssertEqual(MenuBarTimeFormatter.string(for: 86_399), "23:59:59")
        XCTAssertEqual(MenuBarTimeFormatter.string(for: 86_400), "24:00:00")
        XCTAssertEqual(MenuBarTimeFormatter.string(for: 360_000), "100:00:00")
    }

    func testIntervalFloorsSecondsLikeTheTimerOutput() {
        XCTAssertEqual(MenuBarTimeFormatter.string(for: 59.99), "0:59")
        XCTAssertEqual(MenuBarTimeFormatter.string(for: 299.01), "4:59")
        XCTAssertEqual(MenuBarTimeFormatter.string(for: 0.99), "0:00")
    }

    func testIntervalTreatsInvalidValuesAsZero() {
        XCTAssertEqual(MenuBarTimeFormatter.string(for: -5), "0:00")
        XCTAssertEqual(MenuBarTimeFormatter.string(for: .nan), "0:00")
        XCTAssertEqual(MenuBarTimeFormatter.string(for: .infinity), "0:00")
    }

    func testClockFollowsHourStyleAndAMPMSetting() throws {
        let utc = try XCTUnwrap(TimeZone(identifier: "UTC"))
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = utc
        let afternoon = try XCTUnwrap(
            calendar.date(from: DateComponents(year: 2026, month: 1, day: 5, hour: 15, minute: 4, second: 37))
        )

        XCTAssertEqual(
            MenuBarTimeFormatter.clockString(for: afternoon, uses24Hour: false, showAMPM: false, timeZone: utc),
            "3:04"
        )
        XCTAssertEqual(
            MenuBarTimeFormatter.clockString(for: afternoon, uses24Hour: false, showAMPM: true, timeZone: utc),
            "3:04 PM"
        )
        XCTAssertEqual(
            MenuBarTimeFormatter.clockString(for: afternoon, uses24Hour: true, showAMPM: false, timeZone: utc),
            "15:04"
        )
    }

    func testNextChangeForCountdownIsTheFractionLeftInTheCurrentSecond() {
        XCTAssertEqual(
            MenuBarTimeFormatter.secondsUntilNextChange(of: 299.25, countingDown: true),
            0.25,
            accuracy: 0.0001
        )
        XCTAssertEqual(
            MenuBarTimeFormatter.secondsUntilNextChange(of: 5, countingDown: true),
            0,
            accuracy: 0.0001
        )
        XCTAssertEqual(
            MenuBarTimeFormatter.secondsUntilNextChange(of: 0, countingDown: true),
            1,
            accuracy: 0.0001
        )
    }

    func testNextChangeForCountUpIsTheRestOfTheCurrentSecond() {
        XCTAssertEqual(
            MenuBarTimeFormatter.secondsUntilNextChange(of: 12.25, countingDown: false),
            0.75,
            accuracy: 0.0001
        )
        XCTAssertEqual(
            MenuBarTimeFormatter.secondsUntilNextChange(of: 12, countingDown: false),
            1,
            accuracy: 0.0001
        )
    }

    func testNextMinuteIsTheWallClockBoundary() {
        let date = Date(timeIntervalSinceReferenceDate: 600 + 37.5)
        XCTAssertEqual(MenuBarTimeFormatter.secondsUntilNextMinute(after: date), 22.5, accuracy: 0.0001)

        let onTheMinute = Date(timeIntervalSinceReferenceDate: 600)
        XCTAssertEqual(MenuBarTimeFormatter.secondsUntilNextMinute(after: onTheMinute), 60, accuracy: 0.0001)
    }

    func testItemIsShownOnlyWhenEnabledAndPro() {
        XCTAssertTrue(MenuBarController.shouldShowItem(isEnabled: true, isPro: true))
        XCTAssertFalse(MenuBarController.shouldShowItem(isEnabled: true, isPro: false))
        XCTAssertFalse(MenuBarController.shouldShowItem(isEnabled: false, isPro: true))
        XCTAssertFalse(MenuBarController.shouldShowItem(isEnabled: false, isPro: false))
    }

    func testShowInMenuBarDefaultsToOffAndPersistsPerTimer() throws {
        let suiteName = "MenuBarTimerTests.\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suiteName))
        defaults.removePersistentDomain(forName: suiteName)
        defer { defaults.removePersistentDomain(forName: suiteName) }

        let store = LegacySettingsStore(defaults: defaults)
        for kind in TimerKind.allCases {
            XCTAssertFalse(store.loadConfiguration(for: kind).showInMenuBar, "\(kind) should default to off")
        }

        var configuration = store.loadConfiguration(for: .countup2)
        configuration.showInMenuBar = true
        store.saveConfiguration(configuration, for: .countup2)

        XCTAssertEqual(defaults.object(forKey: "ShowInMenuBar_countup2") as? Bool, true)
        XCTAssertTrue(LegacySettingsStore(defaults: defaults).loadConfiguration(for: .countup2).showInMenuBar)
        XCTAssertFalse(LegacySettingsStore(defaults: defaults).loadConfiguration(for: .countup).showInMenuBar)

        configuration.showInMenuBar = false
        store.saveConfiguration(configuration, for: .countup2)
        XCTAssertFalse(LegacySettingsStore(defaults: defaults).loadConfiguration(for: .countup2).showInMenuBar)
    }
}
