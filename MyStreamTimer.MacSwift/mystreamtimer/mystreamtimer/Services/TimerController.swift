import AppKit
import Combine
import Foundation

@MainActor
final class TimerController: ObservableObject, Identifiable {
    let kind: TimerKind

    @Published var minutes: Int
    @Published var seconds: Int
    @Published var useMinutes: Bool
    @Published var finishAt: Date
    @Published var output: String
    @Published var finishText: String
    @Published var fileName: String
    @Published var autoStart: Bool
    @Published var beepAtZero: Bool
    @Published var showAMPM: Bool
    @Published var outputStyle: Int
    @Published private(set) var currentText = ""
    @Published private(set) var isRunning = false
    @Published private(set) var isPaused = false
    @Published private(set) var lastError: String?

    private let settingsStore: LegacySettingsStore
    private let fileAccess: BookmarkFileAccess

    private var startDate = Date()
    private var endDate = Date()
    private var pausedRemaining: TimeInterval = 0
    private var pausedElapsed: TimeInterval = 0
    private var updateTask: Task<Void, Never>?
    private var activityToken: NSObjectProtocol?

    var id: String { kind.rawValue }

    var canPauseResume: Bool {
        isRunning && kind != .time
    }

    var startStopTitle: String {
        isRunning ? "Stop" : "Start"
    }

    var pauseResumeTitle: String {
        isPaused ? "Resume" : "Pause"
    }

    init(kind: TimerKind, settingsStore: LegacySettingsStore, fileAccess: BookmarkFileAccess) {
        self.kind = kind
        self.settingsStore = settingsStore
        self.fileAccess = fileAccess

        let configuration = settingsStore.loadConfiguration(for: kind)
        self.minutes = configuration.minutes
        self.seconds = configuration.seconds
        self.useMinutes = configuration.useMinutes
        self.finishAt = configuration.finishAt
        self.output = configuration.output
        self.finishText = configuration.finishText
        self.fileName = configuration.fileName
        self.autoStart = configuration.autoStart
        self.beepAtZero = configuration.beepAtZero
        self.showAMPM = configuration.showAMPM
        self.outputStyle = configuration.outputStyle

        persist()

        Task { [weak self] in
            guard let self else { return }
            do {
                try self.fileAccess.initializeFile(named: configuration.fileName)
                self.lastError = nil
            } catch {
                self.lastError = "Couldn't prepare \(configuration.fileName): \(error.localizedDescription)"
            }
            if configuration.autoStart {
                self.start()
            }
        }
    }

    func persist() {
        let configuration = TimerConfiguration(
            minutes: minutes,
            seconds: seconds,
            useMinutes: useMinutes,
            finishAt: finishAt,
            output: output,
            finishText: finishText,
            fileName: fileName,
            autoStart: autoStart,
            beepAtZero: beepAtZero,
            showAMPM: showAMPM,
            outputStyle: outputStyle
        )
        settingsStore.saveConfiguration(configuration, for: kind)
    }

    func apply(_ command: URLCommand) {
        switch command.action {
        case .start:
            if isRunning {
                stop(clearOutput: false)
            }
            start(overrideMinutes: command.minutes)

        case .stop:
            stop(clearOutput: true)

        case .add:
            adjustBy(minutes: command.minutes)

        case .subtract:
            adjustBy(minutes: -command.minutes)

        case .pause:
            if canPauseResume, !isPaused {
                pauseResume()
            }

        case .resume:
            if canPauseResume, isPaused {
                pauseResume()
            }

        case .reset:
            reset()
        }
    }

    func start(overrideMinutes: Double? = nil) {
        if outputStyle == 0, kind != .time {
            let test = renderCustomOutput(for: 5)
            if test.isEmpty {
                currentText = "Invalid time format. Use {0:hh\\:mm\\:ss}"
                lastError = currentText
                return
            }
        }

        updateTask?.cancel()
        lastError = nil
        isRunning = true
        isPaused = false
        pausedRemaining = 0
        pausedElapsed = 0

        let now = Date()
        if kind.isCountdown {
            let duration: TimeInterval
            if let overrideMinutes {
                duration = max(0, overrideMinutes) * 60
            } else if useMinutes {
                duration = TimeInterval((minutes * 60) + seconds)
            } else {
                let chosenTime = Calendar.current.dateComponents([.hour, .minute], from: finishAt)
                var target = Calendar.current.dateComponents([.year, .month, .day], from: now)
                target.hour = chosenTime.hour
                target.minute = chosenTime.minute
                target.second = 0
                let targetToday = Calendar.current.date(from: target) ?? now
                let effectiveTarget = targetToday > now
                    ? targetToday
                    : Calendar.current.date(byAdding: .day, value: 1, to: targetToday) ?? targetToday
                duration = max(0, effectiveTarget.timeIntervalSince(now))
            }

            startDate = now
            endDate = now.addingTimeInterval(duration)
        } else if kind.isCountUp {
            startDate = now
            if let overrideMinutes {
                pausedElapsed = max(0, overrideMinutes) * 60
            } else {
                pausedElapsed = TimeInterval((minutes * 60) + seconds)
            }
        }

        activityToken = ProcessInfo.processInfo.beginActivity(
            options: [.userInitiated, .idleDisplaySleepDisabled],
            reason: "My Stream Timer is actively running a stream timer."
        )

        updateTask = Task { [weak self] in
            guard let self else { return }
            await self.runTimerLoop()
        }
    }

    func stop(clearOutput: Bool) {
        updateTask?.cancel()
        updateTask = nil
        isRunning = false
        isPaused = false

        if let activityToken {
            ProcessInfo.processInfo.endActivity(activityToken)
            self.activityToken = nil
        }

        if clearOutput {
            currentText = ""
            do {
                try fileAccess.write(text: "", fileName: fileName)
            } catch {
                lastError = error.localizedDescription
            }
        }
    }

    func pauseResume() {
        guard canPauseResume else { return }

        if !isPaused {
            isPaused = true
            if kind.isCountdown {
                pausedRemaining = max(0, endDate.timeIntervalSinceNow)
            } else if kind.isCountUp {
                pausedElapsed = max(0, Date().timeIntervalSince(startDate) + pausedElapsed)
            }
        } else {
            isPaused = false
            if kind.isCountdown {
                endDate = Date().addingTimeInterval(pausedRemaining)
            } else if kind.isCountUp {
                startDate = Date()
            }
        }
    }

    func addMinute() {
        adjustBy(minutes: 1)
    }

    func reset() {
        guard isRunning else { return }
        stop(clearOutput: true)
        start()
    }

    func adjustBy(minutes delta: Double) {
        guard isRunning else { return }

        if kind.isCountdown {
            endDate = endDate.addingTimeInterval(delta * 60)
        } else if kind.isCountUp {
            pausedElapsed = max(0, pausedElapsed + (delta * 60))
        }
    }

    private func runTimerLoop() async {
        while !Task.isCancelled {
            if isPaused {
                try? await Task.sleep(nanoseconds: 100_000_000)
                continue
            }

            let now = Date()
            let nextText = formattedOutput(now: now)

            if nextText != currentText {
                currentText = nextText
                do {
                    try fileAccess.write(text: nextText, fileName: fileName)
                    lastError = nil
                } catch {
                    lastError = error.localizedDescription
                    currentText = "Unable to save timer output: \(error.localizedDescription)"
                }
            }

            if kind.isCountdown, now >= endDate {
                if beepAtZero {
                    NSSound.beep()
                }
                stop(clearOutput: false)
                break
            }

            try? await Task.sleep(nanoseconds: 100_000_000)
        }
    }

    private func formattedOutput(now: Date) -> String {
        if kind == .time {
            return formattedTimeOutput(now: now)
        }

        if kind.isCountdown {
            let remaining = max(0, endDate.timeIntervalSince(now))
            if remaining <= 0 {
                return finishText
            }
            return formattedInterval(remaining)
        }

        let elapsed = max(0, now.timeIntervalSince(startDate) + pausedElapsed)
        return formattedInterval(elapsed)
    }

    private func formattedTimeOutput(now: Date) -> String {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")

        switch outputStyle {
        case 1:
            formatter.dateFormat = showAMPM ? "h:mm:ss a" : "h:mm:ss"
        case 2:
            formatter.dateFormat = showAMPM ? "H:mm a" : "H:mm"
        case 3:
            formatter.dateFormat = showAMPM ? "H:mm:ss a" : "H:mm:ss"
        default:
            formatter.dateFormat = showAMPM ? "h:mm a" : "h:mm"
        }

        return formatter.string(from: now)
    }

    private func formattedInterval(_ interval: TimeInterval) -> String {
        switch outputStyle {
        case 1:
            return formatAuto(interval)
        case 2:
            let total = Int(floor(interval))
            let formatter = NumberFormatter()
            formatter.numberStyle = .decimal
            formatter.maximumFractionDigits = 0
            return formatter.string(from: NSNumber(value: total)) ?? "\(total)"
        case 3:
            let totalSeconds = Int(floor(interval))
            let mins = totalSeconds / 60
            let secs = totalSeconds % 60
            let formatter = NumberFormatter()
            formatter.numberStyle = .decimal
            formatter.maximumFractionDigits = 0
            let minsStr = formatter.string(from: NSNumber(value: mins)) ?? "\(mins)"
            return "\(minsStr):\(String(format: "%02d", secs))"
        default:
            return renderCustomOutput(for: interval)
        }
    }

    private func formatAuto(_ interval: TimeInterval) -> String {
        let totalSeconds = max(0, Int(floor(interval)))
        let days = totalSeconds / 86_400
        let hours = (totalSeconds % 86_400) / 3_600
        let mins = (totalSeconds % 3_600) / 60
        let secs = totalSeconds % 60

        if days > 0 {
            let dayPadding: Int
            if days >= 10_000 { dayPadding = 5 }
            else if days >= 1_000 { dayPadding = 4 }
            else if days >= 100 { dayPadding = 3 }
            else if days >= 10 { dayPadding = 2 }
            else { dayPadding = 1 }
            let dayStr = String(repeating: "0", count: max(0, dayPadding - String(days).count)) + "\(days)"
            return "\(dayStr):\(String(format: "%02d", hours)):\(String(format: "%02d", mins)):\(String(format: "%02d", secs))"
        }

        if hours > 0 {
            if hours >= 10 {
                return "\(String(format: "%02d", hours)):\(String(format: "%02d", mins)):\(String(format: "%02d", secs))"
            }
            return "\(hours):\(String(format: "%02d", mins)):\(String(format: "%02d", secs))"
        }

        if mins > 0 {
            if mins >= 10 {
                return "\(String(format: "%02d", mins)):\(String(format: "%02d", secs))"
            }
            return "\(mins):\(String(format: "%02d", secs))"
        }

        if secs >= 10 {
            return String(format: "%02d", secs)
        }

        let formatter = NumberFormatter()
        formatter.numberStyle = .decimal
        formatter.maximumFractionDigits = 0
        return formatter.string(from: NSNumber(value: secs)) ?? "\(secs)"
    }

    private func renderCustomOutput(for interval: TimeInterval) -> String {
        let pattern = output.isEmpty ? kind.defaultOutput : output
        guard let regex = try? NSRegularExpression(pattern: #"\{0:([^}]*)\}"#) else {
            return pattern
        }

        let range = NSRange(pattern.startIndex..<pattern.endIndex, in: pattern)
        let matches = regex.matches(in: pattern, range: range)

        guard !matches.isEmpty else {
            return pattern
        }

        var rendered = pattern
        for match in matches.reversed() {
            guard
                let tokenRange = Range(match.range(at: 1), in: rendered),
                let fullRange = Range(match.range(at: 0), in: rendered)
            else {
                continue
            }

            let token = String(rendered[tokenRange])
            let replacement = formattedToken(token, for: interval)
            rendered.replaceSubrange(fullRange, with: replacement)
        }

        return rendered
    }

    private func formattedToken(_ token: String, for interval: TimeInterval) -> String {
        let totalSeconds = max(0, Int(floor(interval)))
        let days = totalSeconds / 86_400
        let hours = (totalSeconds % 86_400) / 3_600
        let mins = (totalSeconds % 3_600) / 60
        let secs = totalSeconds % 60

        var value = token.replacingOccurrences(of: #"\\:"# , with: ":")

        let replacements: [(String, String)] = [
            ("ddddd", String(format: "%05d", days)),
            ("dddd", String(format: "%04d", days)),
            ("ddd", String(format: "%03d", days)),
            ("dd", String(format: "%02d", days)),
            ("d", "\(days)"),
            ("hh", String(format: "%02d", hours)),
            ("h", "\(hours)"),
            ("mm", String(format: "%02d", mins)),
            ("m", "\(mins)"),
            ("ss", String(format: "%02d", secs)),
            ("s", "\(secs)"),
        ]

        for (needle, replacement) in replacements {
            value = value.replacingOccurrences(of: needle, with: replacement)
        }

        return value
    }
}
