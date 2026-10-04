import Foundation
import XCTest
@testable import My_Stream_Timer

@MainActor
final class MenuBarTimerTests: XCTestCase {
    func testItemTitleIsTheTimerOutputWhileRunning() {
        XCTAssertEqual(MenuBarController.itemTitle(output: "04:59", isRunning: true), "04:59")
        XCTAssertEqual(MenuBarController.itemTitle(output: "299", isRunning: true), "299")
        XCTAssertEqual(
            MenuBarController.itemTitle(output: "Starting in 04 min", isRunning: true),
            "Starting in 04 min"
        )
        XCTAssertEqual(MenuBarController.itemTitle(output: "9:41 AM", isRunning: true), "9:41 AM")
    }

    func testItemTitleIsEmptyWhileStopped() {
        XCTAssertEqual(MenuBarController.itemTitle(output: "Finished!", isRunning: false), "")
        XCTAssertEqual(MenuBarController.itemTitle(output: "", isRunning: true), "")
    }

    func testItemTitleStaysOnOneLine() {
        XCTAssertEqual(
            MenuBarController.itemTitle(output: "  Back in\n04:59  \n", isRunning: true),
            "Back in 04:59"
        )
    }

    func testItemTitleIsCutOffWhenTooLongForTheMenuBar() {
        let limit = MenuBarController.maximumTitleLength
        let exact = String(repeating: "8", count: limit)
        XCTAssertEqual(MenuBarController.itemTitle(output: exact, isRunning: true), exact)

        let long = MenuBarController.itemTitle(
            output: "The stream is starting in 04:59, grab a drink and say hello",
            isRunning: true
        )
        XCTAssertEqual(long, "The stream is starting in 04:59…")
        XCTAssertLessThanOrEqual(long.count, limit)
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
            controller.output = "{0:mm:ss}"
            controller.minutes = 5
            controller.seconds = 0

            let actions = MenuBarTimerActions(controller: controller)
            XCTAssertTrue(actions.start())
            controller.adjustBy(minutes: 10)
            try await self.waitForOutput(of: controller) { $0.hasPrefix("14:") }

            XCTAssertTrue(actions.start(), "Start on a running timer reports it as running")
            try await Task.sleep(nanoseconds: 300_000_000)
            XCTAssertTrue(
                controller.currentText.hasPrefix("14:"),
                "Start on a running timer should not restart it, but it shows \(controller.currentText)"
            )

            await actions.stop()?.value
        }
    }

    private func waitForOutput(
        of controller: TimerController,
        timeout: TimeInterval = 5,
        until matches: (String) -> Bool
    ) async throws {
        let deadline = Date().addingTimeInterval(timeout)
        while !matches(controller.currentText) {
            guard Date() < deadline else {
                return XCTFail("Timed out waiting for output; last was \(controller.currentText)")
            }
            try await Task.sleep(nanoseconds: 20_000_000)
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
