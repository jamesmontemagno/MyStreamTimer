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

    func testCountUpCanStartAtZero() throws {
        let command = try XCTUnwrap(
            URLCommand(url: XCTUnwrap(URL(string: "mystreamtimer://countup/?mins=0")))
        )

        XCTAssertEqual(command.kind, .countup)
        XCTAssertEqual(command.action, .start)
        XCTAssertEqual(command.minutes, 0)
    }

    func testCountUpCanStartAtZeroSeconds() throws {
        let command = try XCTUnwrap(
            URLCommand(url: XCTUnwrap(URL(string: "mystreamtimer://countup2/?secs=0")))
        )

        XCTAssertEqual(command.kind, .countup2)
        XCTAssertEqual(command.action, .start)
        XCTAssertEqual(command.minutes, 0)
    }

    func testCountdownCannotStartAtZero() throws {
        let url = try XCTUnwrap(URL(string: "mystreamtimer://countdown/?mins=0"))

        XCTAssertNil(URLCommand(url: url))
    }

    func testNumericCommandsRejectInvalidAndUnrepresentableDurations() throws {
        let parameters = ["mins", "secs", "addmins", "addsecs", "subtractmins", "subtractsecs"]
        let values = ["nan", "inf", "+inf", "-inf", "1e100", "-1e100", "1e309", "invalid", ""]
        for kind in TimerKind.allCases {
            for parameter in parameters {
                for value in values {
                    var components = URLComponents()
                    components.scheme = "mystreamtimer"
                    components.host = kind.rawValue
                    components.queryItems = [URLQueryItem(name: parameter, value: value)]
                    let url = try XCTUnwrap(components.url)
                    XCTAssertNil(URLCommand(url: url), "\(kind): \(parameter)=\(value)")

                    components.queryItems?.append(URLQueryItem(name: "stop", value: nil))
                    XCTAssertNil(
                        URLCommand(url: try XCTUnwrap(components.url)),
                        "Invalid numeric input must not fall through to Stop"
                    )
                }
            }
        }
    }

    func testValidNumericCommandsKeepTheirUnitsAndActions() throws {
        let parameters: [(String, String, CommandAction)] = [
            ("mins", "1.25", .start), ("secs", "75", .start),
            ("addmins", "1.25", .add), ("addsecs", "75", .add),
            ("subtractmins", "1.25", .subtract), ("subtractsecs", "75", .subtract)
        ]
        for kind in TimerKind.allCases {
            for (parameter, value, action) in parameters {
                let url = try XCTUnwrap(URL(string: "mystreamtimer://\(kind.rawValue)/?\(parameter)=\(value)"))
                let command = try XCTUnwrap(URLCommand(url: url))
                XCTAssertEqual(command.kind, kind)
                XCTAssertEqual(command.action, action)
                XCTAssertEqual(command.minutes, 1.25)
            }
        }
    }
}
