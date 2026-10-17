import Foundation
import XCTest
@testable import My_Stream_Timer

@MainActor
final class CommandBoundaryTests: XCTestCase {
    func testSupportedIntervalsMatchSafeIntegerConversion() {
        for interval in [0, Double.leastNonzeroMagnitude, 0.5, 60, Double(Int.max).nextDown] {
            XCTAssertTrue(TimerInterval.isSupported(interval), "\(interval)")
        }
        for interval in [Double.nan, .infinity, -.infinity, -1, Double(Int.max), 1e100] {
            XCTAssertFalse(TimerInterval.isSupported(interval), "\(interval)")
        }
    }

    func testColdAndStoppedAdjustmentsRemainIgnored() async throws {
        for kind in TimerKind.allCases {
            try await withController(kind) { controller in
                let text = controller.currentText
                controller.adjustBy(minutes: -1)
                controller.adjustBy(minutes: 1e100)
                XCTAssertFalse(controller.isRunning)
                XCTAssertEqual(controller.currentText, text)

                controller.start(overrideMinutes: 5)
                await controller.stop(clearOutput: true)
                controller.adjustBy(minutes: -1)
                XCTAssertFalse(controller.isRunning)
                XCTAssertEqual(controller.currentText, "")
            }
        }
    }

    func testInvalidDirectStartsDoNotStartTimers() async throws {
        for kind in TimerKind.allCases where kind != .time {
            try await withController(kind) { controller in
                for minutes in [Double.nan, .infinity, -.infinity, 1e100, Double.greatestFiniteMagnitude, -1] {
                    controller.start(overrideMinutes: minutes)
                    XCTAssertFalse(controller.isRunning, "\(kind), \(minutes)")
                    XCTAssertNotNil(controller.lastError)
                }
            }
        }
    }

    func testInvalidStartsAndAdjustmentsPreservePausedDuration() async throws {
        for kind in TimerKind.allCases where kind != .time {
            try await withController(kind) { controller in
                controller.output = "{0:hh}:{0:mm}"
                controller.start(overrideMinutes: 10)
                try await self.waitForOutput(of: controller, matching: kind.isCountdown ? "00:09" : "00:10")
                controller.pauseResume()
                let text = controller.currentText

                for value in [Double.nan, .infinity, -.infinity, 1e100, -1e100] {
                    controller.start(overrideMinutes: value)
                    XCTAssertTrue(controller.isRunning)
                    XCTAssertTrue(controller.isPaused)
                    XCTAssertNotNil(controller.lastError)
                    controller.adjustBy(minutes: value)
                    XCTAssertTrue(controller.isRunning)
                    XCTAssertTrue(controller.isPaused)
                    XCTAssertEqual(controller.currentText, text)
                    XCTAssertNotNil(controller.lastError)
                }

                controller.adjustBy(minutes: 1)
                controller.pauseResume()
                try await self.waitForOutput(of: controller, matching: kind.isCountdown ? "00:10" : "00:11")
                XCTAssertTrue(controller.isRunning)
                XCTAssertFalse(controller.isPaused)
            }
        }
    }

    func testRejectedURLRestartPreservesRunningAndPausedTimers() async throws {
        for paused in [false, true] {
            try await withController(.countdown) { controller in
                controller.output = "{0:hh}:{0:mm}"
                controller.start(overrideMinutes: 10)
                try await self.waitForOutput(of: controller, matching: "00:09")
                if paused {
                    controller.pauseResume()
                }
                let text = controller.currentText
                controller.output = "{0:}"
                let command = try XCTUnwrap(URLCommand(url: XCTUnwrap(
                    URL(string: "mystreamtimer://countdown/?mins=5")
                )))
                await controller.apply(command)
                XCTAssertTrue(controller.isRunning)
                XCTAssertEqual(controller.isPaused, paused)
                XCTAssertEqual(controller.currentText, text)
                XCTAssertNotNil(controller.lastError)

                controller.output = "{0:hh}:{0:mm}"
                await controller.apply(command)
                XCTAssertTrue(controller.isRunning)
                XCTAssertFalse(controller.isPaused)
                try await self.waitForOutput(of: controller, matching: "00:04")
            }
        }
    }

    func testInvalidStartsAndAdjustmentsPreserveRunningDuration() async throws {
        for kind in TimerKind.allCases where kind != .time {
            try await withController(kind) { controller in
                controller.output = "{0:hh}:{0:mm}"
                controller.start(overrideMinutes: 10)
                try await self.waitForOutput(of: controller, matching: kind.isCountdown ? "00:09" : "00:10")
                for value in [Double.nan, .infinity, -.infinity, 1e100, -1e100] {
                    controller.start(overrideMinutes: value)
                    XCTAssertTrue(controller.isRunning)
                    XCTAssertFalse(controller.isPaused)
                    XCTAssertNotNil(controller.lastError)
                    controller.adjustBy(minutes: value)
                    XCTAssertTrue(controller.isRunning)
                    XCTAssertFalse(controller.isPaused)
                    XCTAssertNotNil(controller.lastError)
                }
                controller.adjustBy(minutes: 1)
                try await self.waitForOutput(of: controller, matching: kind.isCountdown ? "00:10" : "00:11")
            }
        }
    }

    func testExtremeSettingsRejectStartAndResetWithoutIntegerOverflow() async throws {
        for kind in TimerKind.allCases where kind != .time {
            try await withController(kind) { controller in
                controller.useMinutes = true
                controller.start(overrideMinutes: 10)
                controller.pauseResume()
                for value in [Int.max, Int.min] {
                    controller.minutes = value
                    controller.seconds = value
                    await controller.reset()
                    XCTAssertTrue(controller.isRunning)
                    XCTAssertTrue(controller.isPaused)
                    XCTAssertNotNil(controller.lastError)

                    await controller.stop(clearOutput: true)
                    controller.start()
                    XCTAssertFalse(controller.isRunning)
                    XCTAssertNotNil(controller.lastError)
                    controller.start(overrideMinutes: 10)
                    controller.pauseResume()
                }

                controller.minutes = 5
                controller.seconds = 0
                controller.soundMinutes = Int.max
                controller.beepAtZero = false
                await controller.reset()
                XCTAssertTrue(controller.isRunning)
                XCTAssertFalse(controller.isPaused)
            }
        }
    }

    func testCountUpStillStartsAtZero() async throws {
        for kind in TimerKind.allCases where kind.isCountUp {
            try await withController(kind) { controller in
                controller.start(overrideMinutes: 0)
                XCTAssertTrue(controller.isRunning)
                XCTAssertFalse(controller.isPaused)
                XCTAssertNil(controller.lastError)
            }
        }
    }

    private func waitForOutput(
        of controller: TimerController,
        matching text: String
    ) async throws {
        let deadline = Date().addingTimeInterval(5)
        while controller.currentText != text {
            guard Date() < deadline else {
                return XCTFail("Expected \(text), last output was \(controller.currentText)")
            }
            try await Task.sleep(nanoseconds: 20_000_000)
        }
    }

    private func withController(
        _ kind: TimerKind,
        _ body: (TimerController) async throws -> Void
    ) async throws {
        let suiteName = "CommandBoundaryTests.\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suiteName))
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
        await controller.stop(clearOutput: true)
        do {
            try await body(controller)
        } catch {
            await controller.stop(clearOutput: true)
            throw error
        }
        await controller.stop(clearOutput: true)
    }
}
