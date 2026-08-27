import Foundation

struct TimerIconChoice: Identifiable {
    let systemName: String
    let label: String

    var id: String { systemName }
}

enum CommandAction {
    case start
    case stop
    case add
    case subtract
    case pause
    case resume
    case reset
}
enum TimerKind: String, CaseIterable, Identifiable, Codable {
    case countdown
    case countdown2
    case countdown3
    case countdown4
    case countup
    case countup2
    case time

    var id: String { rawValue }

    var title: String {
        switch self {
        case .countdown:
            return "Countdown 1"
        case .countdown2:
            return "Countdown 2"
        case .countdown3:
            return "Countdown 3"
        case .countdown4:
            return "Countdown 4"
        case .countup:
            return "Count Up 1"
        case .countup2:
            return "Count Up 2"
        case .time:
            return "Current Time"
        }
    }

    var shortTitle: String {
        switch self {
        case .countdown:
            return "Down"
        case .countdown2:
            return "Down 2"
        case .countdown3:
            return "Down 3"
        case .countdown4:
            return "Down 4"
        case .countup:
            return "Up"
        case .countup2:
            return "Up 2"
        case .time:
            return "Time"
        }
    }

    var systemImage: String {
        switch self {
        case .countdown, .countdown2, .countdown3, .countdown4:
            return "timer.circle"
        case .countup, .countup2:
            return "arrow.up.circle"
        case .time:
            return "clock"
        }
    }

    func effectiveTitle(displayName: String) -> String {
        let trimmedName = displayName.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmedName.isEmpty ? title : trimmedName
    }

func effectiveSystemImage(iconGlyph: String) -> String {
    let trimmedIcon = iconGlyph.trimmingCharacters(in: .whitespacesAndNewlines)
    guard !trimmedIcon.isEmpty else { return systemImage }

    return Self.iconChoices.contains(where: { $0.systemName == trimmedIcon }) ? trimmedIcon : systemImage
}

    static let iconChoices = [
        TimerIconChoice(systemName: "timer", label: "Stopwatch"),
        TimerIconChoice(systemName: "arrow.up", label: "Up"),
        TimerIconChoice(systemName: "clock", label: "Clock"),
        TimerIconChoice(systemName: "bolt.fill", label: "Lightning"),
        TimerIconChoice(systemName: "star.fill", label: "Star"),
        TimerIconChoice(systemName: "trophy.fill", label: "Trophy"),
        TimerIconChoice(systemName: "gift.fill", label: "Gift"),
        TimerIconChoice(systemName: "gamecontroller.fill", label: "Game"),
        TimerIconChoice(systemName: "music.note", label: "Music"),
        TimerIconChoice(systemName: "video.fill", label: "Video"),
        TimerIconChoice(systemName: "megaphone.fill", label: "Megaphone"),
        TimerIconChoice(systemName: "cup.and.saucer.fill", label: "Coffee"),
        TimerIconChoice(systemName: "airplane", label: "Launch"),
        TimerIconChoice(systemName: "heart.fill", label: "Favorite"),
        TimerIconChoice(systemName: "flag.fill", label: "Flag"),
        TimerIconChoice(systemName: "microphone.fill", label: "Microphone"),
        TimerIconChoice(systemName: "display", label: "Monitor"),
        TimerIconChoice(systemName: "bubble.left.fill", label: "Chat"),
        TimerIconChoice(systemName: "calendar", label: "Calendar"),
        TimerIconChoice(systemName: "book.fill", label: "Book"),
    ]

    var requiresPro: Bool {
        self == .countdown4 || self == .countup2 || self == .time
    }

    var isCountdown: Bool {
        switch self {
        case .countdown, .countdown2, .countdown3, .countdown4:
            return true
        default:
            return false
        }
    }

    var isCountUp: Bool {
        self == .countup || self == .countup2
    }

    var defaultMinutes: Int {
        switch self {
        case .countdown, .countdown2, .countdown3, .countdown4:
            return 5
        case .countup, .countup2, .time:
            return 0
        }
    }

    var defaultSeconds: Int { 0 }

    var defaultOutput: String {
        if isCountUp {
            return "{0:hh:mm:ss}"
        }
        return "Starting in {0:hh:mm:ss}"
    }

    var defaultFinishText: String { "Let's do this!" }

    var defaultFileName: String { "\(rawValue).txt" }

    var outputStyleOptions: [String] {
        if self == .time {
            return [
                "Hour:Minute",
                "Hour:Minute:Second",
                "24-hour Hour:Minute",
                "24-hour Hour:Minute:Second",
            ]
        }

        return [
            "Custom",
            "Auto",
            "Total Seconds",
            "Total Minutes:Seconds",
        ]
    }

    init?(host: String) {
        switch host.lowercased() {
        case "countdown", "countdown1":
            self = .countdown
        case "countdown2":
            self = .countdown2
        case "countdown3":
            self = .countdown3
        case "countdown4":
            self = .countdown4
        case "countup", "countup1":
            self = .countup
        case "countup2":
            self = .countup2
        case "time":
            self = .time
        default:
            return nil
        }
    }
}

struct URLCommand {
    let kind: TimerKind
    let action: CommandAction
    let minutes: Double

    init?(url: URL) {
        guard let host = url.host?.lowercased(), let kind = TimerKind(host: host) else {
            return nil
        }

        let components = URLComponents(url: url, resolvingAgainstBaseURL: false)
        let items = components?.queryItems ?? []
        func value(for name: String) -> String? {
            items.first(where: { $0.name.lowercased() == name })?.value
        }
        func contains(_ name: String) -> Bool {
            items.contains(where: { $0.name.lowercased() == name })
        }

        self.kind = kind

        if let mins = value(for: "mins"), let parsed = Double(mins), parsed > 0 {
            self.action = .start
            self.minutes = parsed
            return
        }

        if let secs = value(for: "secs"), let parsed = Double(secs), parsed > 0 {
            self.action = .start
            self.minutes = parsed / 60
            return
        }

        if contains("topofhour") {
            let now = Date()
            let calendar = Calendar.current
            let minute = calendar.component(.minute, from: now)
            let second = calendar.component(.second, from: now)
            var mins = 60.0 - Double(minute)
            mins += (60.0 - Double(second)) / 60.0
            mins -= 1.0
            self.action = .start
            self.minutes = max(0, mins)
            return
        }

        if let rawTime = value(for: "to"), let mins = URLCommand.minutesUntilClockTime(rawTime) {
            self.action = .start
            self.minutes = mins
            return
        }

        if let mins = value(for: "addmins"), let parsed = Double(mins), parsed > 0 {
            self.action = .add
            self.minutes = parsed
            return
        }

        if let secs = value(for: "addsecs"), let parsed = Double(secs), parsed > 0 {
            self.action = .add
            self.minutes = parsed / 60
            return
        }

        if let mins = value(for: "subtractmins"), let parsed = Double(mins), parsed > 0 {
            self.action = .subtract
            self.minutes = parsed
            return
        }

        if let secs = value(for: "subtractsecs"), let parsed = Double(secs), parsed > 0 {
            self.action = .subtract
            self.minutes = parsed / 60
            return
        }

        if contains("pause") {
            self.action = .pause
            self.minutes = 0
            return
        }

        if contains("resume") {
            self.action = .resume
            self.minutes = 0
            return
        }

        if contains("reset") {
            self.action = .reset
            self.minutes = 0
            return
        }

        if contains("stop") {
            self.action = .stop
            self.minutes = 0
            return
        }

        if kind == .time, contains("start") {
            self.action = .start
            self.minutes = 0
            return
        }

        return nil
    }

    private static func minutesUntilClockTime(_ rawValue: String) -> Double? {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.dateFormat = "H:mm"

        guard let targetTime = formatter.date(from: rawValue) else { return nil }

        let calendar = Calendar.current
        let now = Date()
        let components = calendar.dateComponents([.hour, .minute], from: targetTime)

        guard
            let hour = components.hour,
            let minute = components.minute
        else {
            return nil
        }

        var targetComponents = calendar.dateComponents([.year, .month, .day], from: now)
        targetComponents.hour = hour
        targetComponents.minute = minute
        targetComponents.second = 0

        guard var targetDate = calendar.date(from: targetComponents) else { return nil }
        if targetDate <= now {
            targetDate = calendar.date(byAdding: .day, value: 1, to: targetDate) ?? targetDate
        }

        return targetDate.timeIntervalSince(now) / 60
    }
}

struct TimerConfiguration {
    var minutes: Int
    var seconds: Int
    var useMinutes: Bool
    var finishAt: Date
    var output: String
    var finishText: String
    var fileName: String
    var autoStart: Bool
    var beepAtZero: Bool
    var showAMPM: Bool
    var outputStyle: Int
    var displayName: String
    var iconGlyph: String
}
