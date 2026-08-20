import Foundation
import XCTest
@testable import My_Stream_Timer

@MainActor
final class TimerEngineTests: XCTestCase {
    func testCountdownWritesFinishTextAtCompletion() async throws {
        let directory = try temporaryDirectory()
        defer { try? FileManager.default.removeItem(at: directory) }

        let completed = expectation(description: "timer completed")
        let engine = TimerEngine { event in
            if case .completed = event {
                completed.fulfill()
            }
        }
        let destination = timerDestination(in: directory)
        let now = Date()

        await engine.start(
            TimerEngine.Configuration(
                generation: 1,
                mode: .countdown,
                startDate: now,
                endDate: now.addingTimeInterval(0.05),
                initialElapsed: 0,
                output: "Running",
                finishText: "Finished",
                showAMPM: false,
                outputStyle: 0,
                destination: destination
            )
        )

        await fulfillment(of: [completed], timeout: 2)
        XCTAssertEqual(
            try String(contentsOf: directory.appendingPathComponent("timer.txt")),
            "Finished"
        )
    }

    func testNewerGenerationReplacesPriorOutput() async throws {
        let directory = try temporaryDirectory()
        defer { try? FileManager.default.removeItem(at: directory) }

        let written = expectation(description: "new generation written")
        let engine = TimerEngine { event in
            if case let .writeSucceeded(generation, _, _) = event, generation == 2 {
                written.fulfill()
            }
        }
        let destination = timerDestination(in: directory)
        let now = Date()

        await engine.start(
            TimerEngine.Configuration(
                generation: 1,
                mode: .countUp,
                startDate: now,
                endDate: now,
                initialElapsed: 0,
                output: "Old",
                finishText: "",
                showAMPM: false,
                outputStyle: 0,
                destination: destination
            )
        )
        await engine.start(
            TimerEngine.Configuration(
                generation: 2,
                mode: .countUp,
                startDate: now,
                endDate: now,
                initialElapsed: 0,
                output: "New",
                finishText: "",
                showAMPM: false,
                outputStyle: 0,
                destination: destination
            )
        )

        await fulfillment(of: [written], timeout: 2)
        await engine.invalidate(upThrough: 2)
        XCTAssertEqual(
            try String(contentsOf: directory.appendingPathComponent("timer.txt")),
            "New"
        )
    }

    func testCustomFormatWithMultipleTokensUpdatesEachSecond() async throws {
        let directory = try temporaryDirectory()
        defer { try? FileManager.default.removeItem(at: directory) }

        let advanced = expectation(description: "seconds token advanced")
        let engine = TimerEngine { event in
            if case let .rendered(_, text) = event, text.hasSuffix(":01") {
                advanced.fulfill()
            }
        }
        let now = Date()

        await engine.start(
            TimerEngine.Configuration(
                generation: 1,
                mode: .countUp,
                startDate: now,
                endDate: now,
                initialElapsed: 0,
                output: "{0:mm}:{0:ss}",
                finishText: "",
                showAMPM: false,
                outputStyle: 0,
                destination: timerDestination(in: directory)
            )
        )

        await fulfillment(of: [advanced], timeout: 2)
        await engine.invalidate(upThrough: 1)
    }

    private func temporaryDirectory() throws -> URL {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        return directory
    }

    private func timerDestination(in directory: URL) -> TimerOutputDestination {
        TimerOutputDestination(
            fileName: "timer.txt",
            directory: DirectoryAccessSnapshot(
                path: directory.path,
                defaultPath: directory.path,
                bookmarkData: nil
            )
        )
    }
}
