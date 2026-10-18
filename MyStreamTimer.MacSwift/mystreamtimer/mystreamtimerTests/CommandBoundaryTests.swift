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

    func testURLStopClearsEveryPausedDurationTimerAndCannotResumeIt() async throws {
        for kind in TimerKind.allCases where kind != .time {
            try await withControllerAndDirectory(kind) { controller, directory in
                controller.output = "Running"
                let file = directory.appendingPathComponent(controller.fileName)
                try await self.apply("secs=120", to: controller)
                try await self.waitForCondition("Initial output was not saved") {
                    (try? String(contentsOf: file)) == "Running"
                }
                try await self.apply("pause", to: controller)
                try await self.apply("pause", to: controller)
                XCTAssertTrue(controller.isRunning)
                XCTAssertTrue(controller.isPaused)

                try await self.apply("stop", to: controller)
                XCTAssertFalse(controller.isRunning, "\(kind)")
                XCTAssertFalse(controller.isPaused, "\(kind)")
                XCTAssertFalse(controller.canPauseResume)
                XCTAssertEqual(controller.currentText, "")
                XCTAssertNil(controller.lastError)
                XCTAssertEqual(try String(contentsOf: file), "")

                for command in ["resume", "pause", "reset", "addsecs=60", "subtractsecs=60", "stop"] {
                    try await self.apply(command, to: controller)
                    XCTAssertFalse(controller.isRunning, "\(kind): \(command)")
                    XCTAssertFalse(controller.isPaused)
                    XCTAssertEqual(controller.currentText, "")
                }
                XCTAssertEqual(try String(contentsOf: file), "")

                controller.minutes = 7
                controller.seconds = 0
                controller.useMinutes = true
                controller.output = "{0:mm}"
                controller.start()
                try await self.waitForOutput(of: controller, matching: kind.isCountdown ? "06" : "07")
                XCTAssertFalse(controller.isPaused)
            }
        }
    }

    func testURLPauseResumeIsIdempotentAndResetRestartsConfiguredDuration() async throws {
        for kind in TimerKind.allCases where kind != .time {
            try await withController(kind) { controller in
                controller.minutes = 7
                controller.seconds = 0
                controller.useMinutes = true
                controller.output = "{0:mm}"
                for command in ["pause", "resume", "reset"] {
                    try await self.apply(command, to: controller)
                    XCTAssertFalse(controller.isRunning)
                }

                try await self.apply("secs=120", to: controller)
                try await self.apply("resume", to: controller)
                XCTAssertFalse(controller.isPaused)
                try await self.apply("pause", to: controller)
                try await self.apply("pause", to: controller)
                XCTAssertTrue(controller.isPaused)
                try await self.apply("resume", to: controller)
                try await self.apply("resume", to: controller)
                XCTAssertFalse(controller.isPaused)
                try await self.apply("pause", to: controller)
                try await self.apply("reset", to: controller)
                XCTAssertTrue(controller.isRunning)
                XCTAssertFalse(controller.isPaused)
                try await self.waitForOutput(of: controller, matching: kind.isCountdown ? "06" : "07")
            }
        }
    }

    func testCurrentTimeIgnoresPauseAndResumeButStopClearsOutput() async throws {
        try await withControllerAndDirectory(.time) { controller, directory in
            try await self.apply("start", to: controller)
            try await self.waitForCondition("Current time did not render") {
                !controller.currentText.isEmpty
            }
            for command in ["pause", "resume"] {
                try await self.apply(command, to: controller)
                XCTAssertTrue(controller.isRunning)
                XCTAssertFalse(controller.isPaused)
                XCTAssertFalse(controller.canPauseResume)
            }
            try await self.apply("stop", to: controller)
            XCTAssertFalse(controller.isRunning)
            XCTAssertEqual(controller.currentText, "")
            XCTAssertEqual(try String(contentsOf: directory.appendingPathComponent(controller.fileName)), "")
        }
    }

    func testStaticOutputRecoversAfterAWriteFailureWithoutRestart() async throws {
        try await withControllerAndDirectory(.countup) { controller, directory in
            let file = directory.appendingPathComponent(controller.fileName)
            try FileManager.default.removeItem(at: file)
            try FileManager.default.createDirectory(at: file, withIntermediateDirectories: false)
            controller.output = "Running"
            controller.start()
            try await self.waitForCondition("Write failure was not reported") {
                controller.lastError?.hasPrefix("Unable to save timer output:") == true
            }
            XCTAssertTrue(controller.isRunning)
            XCTAssertEqual(controller.currentText, "Running")

            try FileManager.default.removeItem(at: file)
            try await self.waitForCondition("Successful retry did not clear the error") {
                controller.lastError == nil
            }
            XCTAssertTrue(controller.isRunning)
            XCTAssertEqual(try String(contentsOf: file), "Running")
        }
    }

    func testFailedOutputClearStillStopsAndCanBeRetried() async throws {
        for kind in TimerKind.allCases {
            try await withControllerAndDirectory(kind) { controller, directory in
                let file = directory.appendingPathComponent(controller.fileName)
                try FileManager.default.removeItem(at: file)
                try FileManager.default.createDirectory(at: file, withIntermediateDirectories: false)
                controller.start(overrideMinutes: 2)
                try await self.waitForCondition("Write failure was not reported") {
                    controller.lastError != nil
                }
                if kind != .time {
                    controller.pauseResume()
                }
                try await self.apply("stop", to: controller)
                XCTAssertFalse(controller.isRunning)
                XCTAssertFalse(controller.isPaused)
                XCTAssertEqual(controller.currentText, "")
                XCTAssertNotNil(controller.lastError, "A failed clear must not be silent")
                try await self.apply("resume", to: controller)
                XCTAssertFalse(controller.isRunning)

                try FileManager.default.removeItem(at: file)
                try await self.apply("stop", to: controller)
                XCTAssertNil(controller.lastError)
                XCTAssertEqual(try String(contentsOf: file), "")
            }
        }
    }

    func testRestartOverlappingStopKeepsTheNewTimerRunning() async throws {
        for kind in TimerKind.allCases where kind != .time {
            for blockedOperation in ["write:Old", "write:"] {
                let oldWrite = expectation(description: "old output write started")
                let newWrite = expectation(description: "new output write started")
                let clearWrite = blockedOperation == "write:"
                    ? expectation(description: "output clear started")
                    : nil
                var starts = ["write:Old": oldWrite, "write:New": newWrite]
                if let clearWrite {
                    starts["write:"] = clearWrite
                }
                let io = GatedTimerIO(gatedOperations: [blockedOperation], starts: starts)
                try await withGatedController(kind, io: io) { controller in
                    controller.output = "Old"
                    controller.start(overrideMinutes: 2)
                    await self.fulfillment(of: [oldWrite], timeout: 2)
                    let stopping = Task { await controller.stop(clearOutput: true) }
                    if let clearWrite {
                        await self.fulfillment(of: [clearWrite], timeout: 2)
                    } else {
                        try await self.waitForCondition("Stop did not invalidate the running timer") {
                            !controller.isRunning
                        }
                    }
                    controller.output = "New"
                    controller.start(overrideMinutes: 3)
                    let newWriteStarted = await io.hasStarted("write:New")
                    XCTAssertFalse(newWriteStarted, "The new run must wait for the held write")

                    await io.release(blockedOperation)
                    await stopping.value
                    await self.fulfillment(of: [newWrite], timeout: 2)
                    try await self.waitForOutput(of: controller, matching: "New")
                    XCTAssertTrue(controller.isRunning)
                    XCTAssertFalse(controller.isPaused)
                    await controller.shutdown()
                    let writes = await io.writes
                    XCTAssertEqual(
                        writes,
                        blockedOperation == "write:Old" ? ["Old", "New"] : ["Old", "", "New"]
                    )
                    XCTAssertNil(controller.lastError)
                }
            }
        }
    }

    func testInitializationCompletionCannotReplaceANewerTimerWriteError() async throws {
        for initializationFails in [false, true] {
            let initializing = expectation(description: "initialization is held")
            let io = GatedTimerIO(
                gatedOperations: ["initialize"],
                starts: ["initialize": initializing],
                failWrites: true
            )
            try await withGatedController(.countup, io: io) { controller in
                await self.fulfillment(of: [initializing], timeout: 2)
                controller.output = "Running"
                controller.start()
                try await self.waitForCondition("Newer timer write error was not reported") {
                    controller.lastError?.hasPrefix("Unable to save timer output:") == true
                }
                controller.pauseResume()
                let error = try XCTUnwrap(controller.lastError)
                await io.release("initialize", failing: initializationFails)
                await controller.shutdown()
                XCTAssertEqual(controller.lastError, error)
            }
        }
    }

    func testTerminationDeadlineReportsIncompleteDrainWithoutWaitingForBlockedWrite() async throws {
        let writing = expectation(description: "write is held")
        let io = GatedTimerIO(
            gatedOperations: ["write:Running"],
            starts: ["write:Running": writing]
        )
        try await withGatedController(.countup, io: io) { controller in
            controller.output = "Running"
            controller.start()
            await self.fulfillment(of: [writing], timeout: 2)
            var timeoutReports = 0
            let returned = self.expectation(description: "termination deadline returned")
            let waiting = Task {
                let drained = await AppDelegate.waitForShutdown(
                    timeoutNanoseconds: 20_000_000,
                    shutdown: { await controller.shutdown() },
                    reportTimeout: { timeoutReports += 1 }
                )
                XCTAssertFalse(drained, "A held write must not be reported as drained")
                returned.fulfill()
            }
            await self.fulfillment(of: [returned], timeout: 2)
            let writeStillBlocked = await io.isBlocked("write:Running")
            XCTAssertTrue(writeStillBlocked, "Cancellation must not pretend to finish non-cooperative I/O")
            XCTAssertEqual(timeoutReports, 1)
            XCTAssertFalse(controller.isRunning)
            await io.release("write:Running")
            await waiting.value
            await controller.shutdown()
            XCTAssertEqual(timeoutReports, 1, "Late completion must not report or resume twice")
        }
    }

    func testTerminationDrainCompletesWithoutTimeoutReport() async {
        var timeoutReports = 0
        let drained = await AppDelegate.waitForShutdown(
            shutdown: {},
            reportTimeout: { timeoutReports += 1 }
        )
        XCTAssertTrue(drained)
        XCTAssertEqual(timeoutReports, 0)
    }

    func testShutdownPreservesOutputAndRejectsFurtherCommands() async throws {
        for kind in TimerKind.allCases {
            try await withControllerAndDirectory(kind) { controller, directory in
                controller.output = "Running"
                controller.start(overrideMinutes: 2)
                let file = directory.appendingPathComponent(controller.fileName)
                try await self.waitForCondition("Initial output was not saved") {
                    (try? String(contentsOf: file)) == controller.currentText && !controller.currentText.isEmpty
                }
                if kind != .time {
                    controller.pauseResume()
                }
                let text = controller.currentText
                await controller.shutdown()
                await controller.shutdown()
                XCTAssertFalse(controller.isRunning)
                XCTAssertFalse(controller.isPaused)
                XCTAssertFalse(controller.isPreviewingEndSound)
                for command in ["secs=120", "pause", "resume", "reset", "stop", "addsecs=60"] {
                    try await self.apply(command, to: controller)
                }
                controller.start(overrideMinutes: 2)
                controller.refreshOutputDestination()
                controller.persist()
                await controller.stop(clearOutput: true)
                XCTAssertFalse(controller.isRunning)
                XCTAssertEqual(controller.currentText, text)
                XCTAssertEqual(try String(contentsOf: file), text)
            }
        }
    }

    func testShutdownAfterWriteFailureDoesNotRetryOrClearTheError() async throws {
        for paused in [false, true] {
            try await withControllerAndDirectory(.countup) { controller, directory in
                let file = directory.appendingPathComponent(controller.fileName)
                try FileManager.default.removeItem(at: file)
                try FileManager.default.createDirectory(at: file, withIntermediateDirectories: false)
                controller.output = "Running"
                controller.start()
                try await self.waitForCondition("Write failure was not reported") {
                    controller.lastError != nil
                }
                if paused {
                    controller.pauseResume()
                }
                let error = controller.lastError
                await controller.shutdown()
                try FileManager.default.removeItem(at: file)
                try await Task.sleep(nanoseconds: 1_100_000_000)
                XCTAssertFalse(FileManager.default.fileExists(atPath: file.path))
                XCTAssertFalse(controller.isRunning)
                XCTAssertFalse(controller.isPaused)
                XCTAssertEqual(controller.currentText, "Running")
                XCTAssertEqual(controller.lastError, error)
            }
        }
    }

    private func apply(_ query: String, to controller: TimerController) async throws {
        let command = try XCTUnwrap(URLCommand(url: XCTUnwrap(
            URL(string: "mystreamtimer://\(controller.kind.rawValue)/?\(query)")
        )))
        await controller.apply(command)
    }

    private func waitForCondition(_ message: String, until condition: () -> Bool) async throws {
        let deadline = Date().addingTimeInterval(5)
        while !condition() {
            guard Date() < deadline else { return XCTFail(message) }
            try await Task.sleep(nanoseconds: 20_000_000)
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
        try await withControllerAndDirectory(kind) { controller, _ in
            try await body(controller)
        }
    }

    private func withGatedController(
        _ kind: TimerKind,
        io: GatedTimerIO,
        _ body: (TimerController) async throws -> Void
    ) async throws {
        let suiteName = "CommandBoundaryTests.Gated.\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suiteName))
        defer { defaults.removePersistentDomain(forName: suiteName) }
        let store = LegacySettingsStore(defaults: defaults)
        let controller = TimerController(
            kind: kind,
            settingsStore: store,
            fileAccess: BookmarkFileAccess(settingsStore: store),
            initializeFile: { try await io.initializeFile(named: $0) },
            writeTimerOutput: { text, _ in try await io.write(text: text) },
            canUseProFeatures: { true }
        )
        do {
            try await body(controller)
        } catch {
            await io.releaseAll()
            await controller.shutdown()
            throw error
        }
        await io.releaseAll()
        await controller.shutdown()
    }

    private func withControllerAndDirectory(
        _ kind: TimerKind,
        _ body: (TimerController, URL) async throws -> Void
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
            try await body(controller, directory)
        } catch {
            await controller.stop(clearOutput: true)
            throw error
        }
        await controller.stop(clearOutput: true)
    }

    private actor GatedTimerIO {
        private var gatedOperations: Set<String>
        private let starts: [String: XCTestExpectation]
        private let failWrites: Bool
        private var started = Set<String>()
        private var pending: [String: CheckedContinuation<Void, Error>] = [:]
        private(set) var writes: [String] = []

        init(
            gatedOperations: Set<String>,
            starts: [String: XCTestExpectation],
            failWrites: Bool = false
        ) {
            self.gatedOperations = gatedOperations
            self.starts = starts
            self.failWrites = failWrites
        }

        func initializeFile(named _: String) async throws {
            try await perform("initialize")
        }

        func write(text: String) async throws -> Data? {
            try await perform("write:\(text)")
            if failWrites {
                throw CocoaError(.fileWriteNoPermission)
            }
            writes.append(text)
            return nil
        }

        func hasStarted(_ operation: String) -> Bool {
            started.contains(operation)
        }

        func isBlocked(_ operation: String) -> Bool {
            pending[operation] != nil
        }

        func release(_ operation: String, failing: Bool = false) {
            gatedOperations.remove(operation)
            guard let continuation = pending.removeValue(forKey: operation) else { return }
            if failing {
                continuation.resume(throwing: CocoaError(.fileWriteNoPermission))
            } else {
                continuation.resume(returning: ())
            }
        }

        func releaseAll() {
            gatedOperations.removeAll()
            let continuations = Array(pending.values)
            pending.removeAll()
            for continuation in continuations {
                continuation.resume(returning: ())
            }
        }

        private func perform(_ operation: String) async throws {
            if started.insert(operation).inserted {
                starts[operation]?.fulfill()
            }
            if gatedOperations.contains(operation) {
                try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Void, Error>) in
                    pending[operation] = continuation
                }
            }
        }
    }
}
