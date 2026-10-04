import Foundation

/// Compact time text for menu bar items. It is derived from the timer itself rather than
/// the timer's output format, so custom text never ends up in the menu bar.
enum MenuBarTimeFormatter {
    /// `m:ss` below an hour and `h:mm:ss` from one hour. Seconds are floored, matching
    /// `TimerEngine`, so the menu bar and the output file change on the same second.
    static func string(for interval: TimeInterval) -> String {
        let totalSeconds = interval.isFinite ? Int(max(0, interval.rounded(.down))) : 0
        let hours = totalSeconds / 3_600
        let minutes = (totalSeconds % 3_600) / 60
        let seconds = totalSeconds % 60

        if hours > 0 {
            return "\(hours):\(String(format: "%02d", minutes)):\(String(format: "%02d", seconds))"
        }
        return "\(minutes):\(String(format: "%02d", seconds))"
    }

    /// The clock without seconds, in the same 12 or 24-hour style the clock timer writes to its file.
    static func clockString(
        for date: Date,
        uses24Hour: Bool,
        showAMPM: Bool,
        timeZone: TimeZone = .current
    ) -> String {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = timeZone
        formatter.dateFormat = (uses24Hour ? "H:mm" : "h:mm") + (showAMPM ? " a" : "")
        return formatter.string(from: date)
    }

    /// Seconds until `string(for:)` next returns a different value for a running timer.
    static func secondsUntilNextChange(of interval: TimeInterval, countingDown: Bool) -> TimeInterval {
        guard interval.isFinite else { return 1 }

        let clamped = max(0, interval)
        let fraction = clamped - clamped.rounded(.down)
        if countingDown {
            return clamped > 0 ? fraction : 1
        }
        return 1 - fraction
    }

    /// Seconds until the wall clock reaches its next whole minute.
    static func secondsUntilNextMinute(after date: Date) -> TimeInterval {
        let reference = date.timeIntervalSinceReferenceDate
        return ((reference / 60).rounded(.down) + 1) * 60 - reference
    }
}
