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

    func testStaleStopActionDoesNotStartAFinishedTimer() async throws {
        try await withController(.countdown) { controller in
            let actions = MenuBarTimerActions(controller: controller)

            XCTAssertNil(actions.stop(), "Stop on a stopped timer should do nothing")
            XCTAssertFalse(controller.isRunning)

            XCTAssertTrue(actions.start())
            XCTAssertTrue(controller.isRunning)

            await actions.stop()?.value
            XCTAssertFalse(controller.isRunning)

            // The menu was opened while the timer ran, so it still offers Stop.
            XCTAssertNil(actions.stop())
            XCTAssertFalse(controller.isRunning)
        }
    }

    func testStaleStartActionDoesNotRestartARunningTimer() async throws {
        try await withController(.countdown) { controller in
            let actions = MenuBarTimerActions(controller: controller)
            XCTAssertTrue(actions.start())
            controller.adjustBy(minutes: 10)
            let adjusted = try XCTUnwrap(controller.displayInterval())

            XCTAssertTrue(actions.start(), "Start on a running timer reports it as running")
            XCTAssertEqual(
                try XCTUnwrap(controller.displayInterval()),
                adjusted,
                accuracy: 5,
                "Start on a running timer should not restart it"
            )

            await actions.stop()?.value
        }
    }

    func testStalePauseAndResumeActionsDoNotFlipTheTimer() async throws {
        try await withController(.countup) { controller in
            let actions = MenuBarTimerActions(controller: controller)

            actions.pause()
            actions.resume()
            XCTAssertFalse(controller.isRunning, "Pause and Resume do nothing to a stopped timer")

            XCTAssertTrue(actions.start())
            actions.resume()
            XCTAssertFalse(controller.isPaused, "Resume should not pause a running timer")

            actions.pause()
            XCTAssertTrue(controller.isPaused)
            actions.pause()
            XCTAssertTrue(controller.isPaused, "Pause should not resume a paused timer")

            actions.resume()
            XCTAssertFalse(controller.isPaused)
            XCTAssertTrue(controller.isRunning)

            await actions.stop()?.value
        }
    }

    private func withController(
        _ kind: TimerKind,
        _ body: (TimerController) async throws -> Void
    ) async throws {
        let suiteName = "MenuBarTimerTests.\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suiteName))
        defaults.removePersistentDomain(forName: suiteName)
        defer { defaults.removePersistentDomain(forName: suiteName) }

        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(suiteName, isDirectory: true)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        defaults.set(directory.path, forKey: "global_directory_path")

        let store = LegacySettingsStore(defaults: defaults)
        let controller = TimerController(
            kind: kind,
            settingsStore: store,
            fileAccess: BookmarkFileAccess(settingsStore: store),
            canUseProFeatures: { true }
        )
        try await body(controller)
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
