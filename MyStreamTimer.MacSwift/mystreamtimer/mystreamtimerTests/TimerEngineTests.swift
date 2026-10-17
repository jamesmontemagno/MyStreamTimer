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
                destination: destination,
                countUpSoundTarget: nil
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
                destination: destination,
                countUpSoundTarget: nil
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
                destination: destination,
                countUpSoundTarget: nil
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
            if case let .rendered(_, text, _) = event, text.hasSuffix(":01") {
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
                destination: timerDestination(in: directory),
                countUpSoundTarget: nil
            )
        )

        await fulfillment(of: [advanced], timeout: 2)
        await engine.invalidate(upThrough: 1)
    }

    func testInvalidIntervalsReportFailureBeforeFormattingOrWriting() async throws {
        let directory = try temporaryDirectory()
        defer { try? FileManager.default.removeItem(at: directory) }

        for mode in [TimerEngine.Mode.countdown, .countUp] {
            for interval in [Double.nan, .infinity, -.infinity, 1e100, -1e100] {
                let failed = expectation(description: "invalid duration reported")
                let engine = TimerEngine { event in
                    if case let .failed(generation, message) = event {
                        XCTAssertEqual(generation, 1)
                        XCTAssertFalse(message.isEmpty)
                        failed.fulfill()
                    } else {
                        XCTFail("Invalid timing must not render, write, or complete normally")
                    }
                }
                let now = Date()
                await engine.start(TimerEngine.Configuration(
                    generation: 1,
                    mode: mode,
                    startDate: now,
                    endDate: now.addingTimeInterval(interval),
                    initialElapsed: interval,
                    output: "{0:hh:mm:ss}",
                    finishText: "Finished",
                    showAMPM: false,
                    outputStyle: 2,
                    destination: timerDestination(in: directory),
                    countUpSoundTarget: nil
                ))
                await fulfillment(of: [failed], timeout: 2)
                await engine.invalidate(upThrough: 1)
                XCTAssertFalse(FileManager.default.fileExists(
                    atPath: directory.appendingPathComponent("timer.txt").path
                ))
            }
        }
    }

    func testLongStaticCountdownUsesBoundedSleep() async throws {
        let directory = try temporaryDirectory()
        defer { try? FileManager.default.removeItem(at: directory) }
        let written = expectation(description: "long countdown written")
        var renderedCount = 0
        let engine = TimerEngine { event in
            switch event {
            case .rendered:
                renderedCount += 1
            case .writeSucceeded:
                written.fulfill()
            case .writeFailed, .failed, .completed:
                XCTFail("A supported long countdown should keep running")
            }
        }
        let now = Date()
        await engine.start(TimerEngine.Configuration(
            generation: 1,
            mode: .countdown,
            startDate: now,
            endDate: now.addingTimeInterval(100_000_000_000),
            initialElapsed: 0,
            output: "Running",
            finishText: "Finished",
            showAMPM: false,
            outputStyle: 0,
            destination: timerDestination(in: directory),
            countUpSoundTarget: nil
        ))
        await fulfillment(of: [written], timeout: 2)
        try await Task.sleep(nanoseconds: 30_000_000)
        await engine.invalidate(upThrough: 1)
        XCTAssertEqual(renderedCount, 1)
        XCTAssertEqual(try String(contentsOf: directory.appendingPathComponent("timer.txt")), "Running")
    }

    func testLargeDayCountsAreNotTruncatedTo32Bits() async throws {
        let directory = try temporaryDirectory()
        defer { try? FileManager.default.removeItem(at: directory) }
        let days = Int(Int32.max) + 100
        for style in [0, 1] {
            let rendered = expectation(description: "large day count rendered")
            let engine = TimerEngine { event in
                if case let .rendered(_, text, _) = event {
                    XCTAssertTrue(text.hasPrefix("\(days)"), text)
                    rendered.fulfill()
                }
                if case .failed = event {
                    XCTFail("The large but representable duration should be supported")
                }
            }
            let now = Date()
            await engine.start(TimerEngine.Configuration(
                generation: 1,
                mode: .countUp,
                startDate: now,
                endDate: now,
                initialElapsed: TimeInterval(days) * 86_400,
                output: "{0:dd}",
                finishText: "",
                showAMPM: false,
                outputStyle: style,
                destination: timerDestination(in: directory),
                countUpSoundTarget: nil
            ))
            await fulfillment(of: [rendered], timeout: 2)
            await engine.invalidate(upThrough: 1)
        }
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
