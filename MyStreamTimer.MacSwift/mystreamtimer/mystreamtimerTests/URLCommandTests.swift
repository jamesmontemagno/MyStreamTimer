import Foundation
import XCTest
@testable import My_Stream_Timer

@MainActor
final class URLCommandTests: XCTestCase {
    func testCurrentTimeStartCommand() throws {
        let command = try XCTUnwrap(URLCommand(url: XCTUnwrap(URL(string: "mystreamtimer://time/?start"))))

        XCTAssertEqual(command.kind, .time)
        XCTAssertEqual(command.action, .start)
        XCTAssertEqual(command.minutes, 0)
    }

    func testCurrentTimeStopCommand() throws {
        let command = try XCTUnwrap(URLCommand(url: XCTUnwrap(URL(string: "mystreamtimer://time/?stop"))))

        XCTAssertEqual(command.kind, .time)
        XCTAssertEqual(command.action, .stop)
        XCTAssertEqual(command.minutes, 0)
    }
}
