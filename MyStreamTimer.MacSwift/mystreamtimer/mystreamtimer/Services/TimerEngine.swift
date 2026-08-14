import Foundation

actor TimerEngine {
    enum Mode: Sendable {
        case countdown
        case countUp
        case time
    }

    struct Configuration: Sendable {
        let generation: UInt64
        let mode: Mode
        let startDate: Date
        let endDate: Date
        let initialElapsed: TimeInterval
        let output: String
        let finishText: String
        let showAMPM: Bool
        let outputStyle: Int
        let destination: TimerOutputDestination
    }

    enum Event: Sendable {
        case rendered(generation: UInt64, text: String)
        case writeSucceeded(
            generation: UInt64,
            refreshedBookmark: Data?,
            destination: TimerOutputDestination
        )
        case writeFailed(generation: UInt64, message: String)
        case completed(generation: UInt64)
    }

    private let worker = BookmarkFileWorker.shared
    private let eventHandler: @MainActor @Sendable (Event) -> Void
    private var newestGeneration: UInt64 = 0
    private var activeGeneration: UInt64?
    private var runTask: Task<Void, Never>?

    init(eventHandler: @escaping @MainActor @Sendable (Event) -> Void) {
        self.eventHandler = eventHandler
    }

    func start(_ configuration: Configuration) {
        guard configuration.generation > newestGeneration else { return }

        newestGeneration = configuration.generation
        activeGeneration = configuration.generation
        runTask?.cancel()
        runTask = Task { [weak self] in
            await self?.run(configuration)
        }
    }

    func invalidate(upThrough generation: UInt64) {
        newestGeneration = max(newestGeneration, generation)
        guard let activeGeneration, activeGeneration <= generation else { return }

        runTask?.cancel()
        runTask = nil
        self.activeGeneration = nil
    }

    private func run(_ configuration: Configuration) async {
        var lastSuccessfullyWrittenText: String?
        var shouldRetryWrite = false

        while !Task.isCancelled, isCurrent(configuration.generation) {
            let now = Date()
            let text = formattedOutput(configuration, now: now)
            await eventHandler(.rendered(generation: configuration.generation, text: text))

            guard !Task.isCancelled, isCurrent(configuration.generation) else { return }

            if text != lastSuccessfullyWrittenText {
                do {
                    let refreshedBookmark = try await worker.writeTimerOutput(
                        text: text,
                        destination: configuration.destination
                    )
                    guard !Task.isCancelled, isCurrent(configuration.generation) else { return }

                    lastSuccessfullyWrittenText = text
                    shouldRetryWrite = false
                    await eventHandler(
                        .writeSucceeded(
                            generation: configuration.generation,
                            refreshedBookmark: refreshedBookmark,
                            destination: configuration.destination
                        )
                    )
                } catch {
                    guard !Task.isCancelled, isCurrent(configuration.generation) else { return }

                    shouldRetryWrite = true
                    await eventHandler(
                        .writeFailed(
                            generation: configuration.generation,
                            message: error.localizedDescription
                        )
                    )
                }
            }

            if configuration.mode == .countdown, now >= configuration.endDate {
                guard isCurrent(configuration.generation) else { return }
                activeGeneration = nil
                runTask = nil
                await eventHandler(.completed(generation: configuration.generation))
                return
            }

            let nextTransition = nextTransitionDate(for: configuration, after: now)
            let retryDate = shouldRetryWrite
                ? now.addingTimeInterval(1)
                : nextTransition
            let wakeDate = min(nextTransition, retryDate)
            let delay = max(0.01, wakeDate.timeIntervalSinceNow)
            try? await Task.sleep(nanoseconds: UInt64(delay * 1_000_000_000))
        }
    }

    private func isCurrent(_ generation: UInt64) -> Bool {
        activeGeneration == generation && newestGeneration == generation
    }

    private func nextTransitionDate(
        for configuration: Configuration,
        after now: Date
    ) -> Date {
        if configuration.mode == .countdown, now >= configuration.endDate {
            return now
        }

        let cadence = outputCadence(for: configuration)
        switch cadence {
        case .static:
            return configuration.mode == .countdown
                ? configuration.endDate
                : now.addingTimeInterval(86_400)

        case .second:
            if configuration.mode == .time {
                return nextWallClockBoundary(after: now, interval: 1)
            }
            return nextElapsedBoundary(
                for: configuration,
                after: now,
                interval: 1
            )

        case .minute:
            if configuration.mode == .time {
                return nextWallClockBoundary(after: now, interval: 60)
            }
            return nextElapsedBoundary(
                for: configuration,
                after: now,
                interval: 60
            )

        case .hour:
            return nextElapsedBoundary(
                for: configuration,
                after: now,
                interval: 3_600
            )

        case .day:
            return nextElapsedBoundary(
                for: configuration,
                after: now,
                interval: 86_400
            )
        }
    }

    private func nextWallClockBoundary(after now: Date, interval: TimeInterval) -> Date {
        let next = (floor(now.timeIntervalSinceReferenceDate / interval) + 1) * interval
        return Date(timeIntervalSinceReferenceDate: next)
    }

    private func nextElapsedBoundary(
        for configuration: Configuration,
        after now: Date,
        interval: TimeInterval
    ) -> Date {
        switch configuration.mode {
        case .countdown:
            let remaining = max(0, configuration.endDate.timeIntervalSince(now))
            let renderedUnit = floor(remaining / interval)
            return configuration.endDate
                .addingTimeInterval(-(renderedUnit * interval) + 0.01)

        case .countUp:
            let elapsed = max(
                0,
                now.timeIntervalSince(configuration.startDate) + configuration.initialElapsed
            )
            let nextUnit = (floor(elapsed / interval) + 1) * interval
            return configuration.startDate
                .addingTimeInterval(nextUnit - configuration.initialElapsed)

        case .time:
            return now.addingTimeInterval(interval)
        }
    }

    private func outputCadence(for configuration: Configuration) -> Cadence {
        if configuration.mode == .time {
            return configuration.outputStyle == 0 || configuration.outputStyle == 2
                ? .minute
                : .second
        }

        guard configuration.outputStyle == 0 else { return .second }
        let pattern = configuration.output.isEmpty ? "{0:hh:mm:ss}" : configuration.output
        guard let regex = try? NSRegularExpression(pattern: #"\{0:([^}]*)\}"#) else {
            return .static
        }

        let range = NSRange(pattern.startIndex..<pattern.endIndex, in: pattern)
        let tokens = regex.matches(in: pattern, range: range).compactMap {
            Range($0.range(at: 1), in: pattern).map { String(pattern[$0]) }
        }
        if tokens.contains(where: { $0.contains("s") }) { return .second }
        if tokens.contains(where: { $0.contains("m") }) { return .minute }
        if tokens.contains(where: { $0.contains("h") }) { return .hour }
        if tokens.contains(where: { $0.contains("d") }) { return .day }
        return .static
    }

    private func formattedOutput(_ configuration: Configuration, now: Date) -> String {
        switch configuration.mode {
        case .time:
            return formattedTimeOutput(configuration, now: now)

        case .countdown:
            let remaining = max(0, configuration.endDate.timeIntervalSince(now))
            return remaining <= 0
                ? configuration.finishText
                : formattedInterval(remaining, configuration: configuration)

        case .countUp:
            let elapsed = max(
                0,
                now.timeIntervalSince(configuration.startDate) + configuration.initialElapsed
            )
            return formattedInterval(elapsed, configuration: configuration)
        }
    }

    private func formattedTimeOutput(_ configuration: Configuration, now: Date) -> String {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")

        switch configuration.outputStyle {
        case 1:
            formatter.dateFormat = configuration.showAMPM ? "h:mm:ss a" : "h:mm:ss"
        case 2:
            formatter.dateFormat = configuration.showAMPM ? "H:mm a" : "H:mm"
        case 3:
            formatter.dateFormat = configuration.showAMPM ? "H:mm:ss a" : "H:mm:ss"
        default:
            formatter.dateFormat = configuration.showAMPM ? "h:mm a" : "h:mm"
        }

        return formatter.string(from: now)
    }

    private func formattedInterval(
        _ interval: TimeInterval,
        configuration: Configuration
    ) -> String {
        switch configuration.outputStyle {
        case 1:
            return formatAuto(interval)
        case 2:
            return formattedNumber(Int(floor(interval)))
        case 3:
            let totalSeconds = Int(floor(interval))
            return "\(formattedNumber(totalSeconds / 60)):\(String(format: "%02d", totalSeconds % 60))"
        default:
            return renderCustomOutput(
                configuration.output.isEmpty ? "{0:hh:mm:ss}" : configuration.output,
                interval: interval
            )
        }
    }

    private func formatAuto(_ interval: TimeInterval) -> String {
        let totalSeconds = max(0, Int(floor(interval)))
        let days = totalSeconds / 86_400
        let hours = (totalSeconds % 86_400) / 3_600
        let mins = (totalSeconds % 3_600) / 60
        let secs = totalSeconds % 60

        if days > 0 {
            let padding = days >= 10_000 ? 5 : days >= 1_000 ? 4 : days >= 100 ? 3 : days >= 10 ? 2 : 1
            return "\(String(format: "%0\(padding)d", days)):\(String(format: "%02d", hours)):\(String(format: "%02d", mins)):\(String(format: "%02d", secs))"
        }
        if hours > 0 {
            let hourText = hours >= 10 ? String(format: "%02d", hours) : "\(hours)"
            return "\(hourText):\(String(format: "%02d", mins)):\(String(format: "%02d", secs))"
        }
        if mins > 0 {
            let minuteText = mins >= 10 ? String(format: "%02d", mins) : "\(mins)"
            return "\(minuteText):\(String(format: "%02d", secs))"
        }
        return secs >= 10 ? String(format: "%02d", secs) : formattedNumber(secs)
    }

    private func renderCustomOutput(_ pattern: String, interval: TimeInterval) -> String {
        guard let regex = try? NSRegularExpression(pattern: #"\{0:([^}]*)\}"#) else {
            return pattern
        }

        let range = NSRange(pattern.startIndex..<pattern.endIndex, in: pattern)
        var rendered = pattern
        for match in regex.matches(in: pattern, range: range).reversed() {
            guard
                let tokenRange = Range(match.range(at: 1), in: rendered),
                let fullRange = Range(match.range(at: 0), in: rendered)
            else {
                continue
            }
            rendered.replaceSubrange(
                fullRange,
                with: formattedToken(String(rendered[tokenRange]), interval: interval)
            )
        }
        return rendered
    }

    private func formattedToken(_ token: String, interval: TimeInterval) -> String {
        let totalSeconds = max(0, Int(floor(interval)))
        let days = totalSeconds / 86_400
        let hours = (totalSeconds % 86_400) / 3_600
        let mins = (totalSeconds % 3_600) / 60
        let secs = totalSeconds % 60
        var value = token.replacingOccurrences(of: #"\\:"# , with: ":")

        for (needle, replacement) in [
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
        ] {
            value = value.replacingOccurrences(of: needle, with: replacement)
        }
        return value
    }

    private func formattedNumber(_ value: Int) -> String {
        let formatter = NumberFormatter()
        formatter.numberStyle = .decimal
        formatter.maximumFractionDigits = 0
        return formatter.string(from: NSNumber(value: value)) ?? "\(value)"
    }

    private enum Cadence {
        case second
        case minute
        case hour
        case day
        case `static`
    }
}
