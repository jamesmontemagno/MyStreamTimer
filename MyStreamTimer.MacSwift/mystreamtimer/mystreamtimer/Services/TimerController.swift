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
    @Published var endSound: EndSound
    @Published var customEndSoundBookmark: Data?
    @Published private(set) var customEndSoundFileName: String
    @Published var soundMinutes: Int
    @Published var soundSeconds: Int
    @Published var showAMPM: Bool
    @Published var outputStyle: Int
    @Published var displayName: String
    @Published var iconGlyph: String
    @Published var showInMenuBar: Bool
    @Published private(set) var currentText = ""
    @Published private(set) var isRunning = false
    @Published private(set) var isPaused = false
    @Published private(set) var lastError: String?

    private let settingsStore: LegacySettingsStore
    private let fileAccess: BookmarkFileAccess
    private let canUseProFeatures: () -> Bool
    private lazy var endSoundPlayer = EndSoundPlayer()

    private var startDate = Date()
    private var endDate = Date()
    private var pausedRemaining: TimeInterval = 0
    private var pausedElapsed: TimeInterval = 0
    private var generation: UInt64 = 0
    private var activeGeneration: UInt64?
    private var activityToken: NSObjectProtocol?
    private var cancellables = Set<AnyCancellable>()
    private var activeSoundEnabled = false
    private var activeEndSound: EndSound = .defaultBeep
    private var activeCustomEndSoundBookmark: Data?
    private var activeCountUpSoundTarget: TimeInterval = 0
    private var previousCountUpSoundElapsed: TimeInterval = 0
    private var didPlayCountUpSound = false
    private lazy var timerEngine = TimerEngine { [weak self] event in
        self?.handleTimerEvent(event)
    }

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

    var isPreviewingEndSound: Bool {
        endSoundPlayer.isPlaying
    }

    var effectiveOutputStyle: Int {
        outputStyle > 0 && !canUseProFeatures() ? 0 : outputStyle
    }

    var effectiveTitle: String {
        kind.effectiveTitle(displayName: displayName)
    }

    var effectiveSystemImage: String {
        kind.effectiveSystemImage(iconGlyph: iconGlyph)
    }

    init(
        kind: TimerKind,
        settingsStore: LegacySettingsStore,
        fileAccess: BookmarkFileAccess,
        canUseProFeatures: @escaping () -> Bool
    ) {
        self.kind = kind
        self.settingsStore = settingsStore
        self.fileAccess = fileAccess
        self.canUseProFeatures = canUseProFeatures

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
        self.endSound = configuration.endSound
        self.customEndSoundBookmark = configuration.customEndSoundBookmark
        self.customEndSoundFileName = configuration.customEndSoundFileName
        self.soundMinutes = configuration.soundMinutes
        self.soundSeconds = configuration.soundSeconds
        self.showAMPM = configuration.showAMPM
        self.outputStyle = configuration.outputStyle
        self.displayName = configuration.displayName
        self.iconGlyph = configuration.iconGlyph
        self.showInMenuBar = configuration.showInMenuBar

        endSoundPlayer.objectWillChange
            .sink { [weak self] _ in
                self?.objectWillChange.send()
            }
            .store(in: &cancellables)
        endSoundPlayer.onPlaybackError = { [weak self] message in
            self?.lastError = message
        }

        persist()

        Task { [weak self] in
            guard let self else { return }
            do {
                try await self.fileAccess.initializeFile(named: configuration.fileName)
                self.lastError = nil
            } catch {
                self.lastError = "Couldn't prepare \(configuration.fileName): \(error.localizedDescription)"
            }
        }
    }

    func persist(restartTimer: Bool = true) {
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
            endSound: endSound,
            customEndSoundBookmark: customEndSoundBookmark,
            customEndSoundFileName: customEndSoundFileName,
            soundMinutes: soundMinutes,
            soundSeconds: soundSeconds,
            showAMPM: showAMPM,
            outputStyle: outputStyle,
            displayName: displayName,
            iconGlyph: iconGlyph,
            showInMenuBar: showInMenuBar
        )
        settingsStore.saveConfiguration(configuration, for: kind)

        if restartTimer, isRunning, !isPaused {
            launchTimerEngine()
        }
    }

    func selectEndSound(_ sound: EndSound) throws {
        stopEndSoundPreview()
        if sound == .custom, !endSoundPlayer.hasUsableCustomSound(bookmark: customEndSoundBookmark) {
            try chooseCustomEndSound()
        } else {
            endSound = sound
            persist(restartTimer: false)
        }
    }

    func chooseCustomEndSound() throws {
        stopEndSoundPreview()
        guard let customFile = try endSoundPlayer.chooseCustomSound() else { return }
        saveCustomEndSound(customFile)
    }

    func selectCustomEndSoundFile(_ url: URL?) throws {
        guard let customFile = try endSoundPlayer.selectCustomFile(url) else { return }
        saveCustomEndSound(customFile)
    }

    @discardableResult
    func previewEndSound() throws -> Bool {
        if isPreviewingEndSound {
            stopEndSoundPreview()
            return false
        }
        return try endSoundPlayer.play(
            sound: endSound,
            customBookmark: customEndSoundBookmark,
            updateCustomBookmark: { [weak self] bookmark in
                self?.updateCustomEndSoundBookmark(bookmark)
            }
        )
    }

    func stopEndSoundPreview() {
        endSoundPlayer.stop()
    }

    func apply(_ command: URLCommand) async {
        switch command.action {
        case .start:
            if isRunning {
                await stop(clearOutput: false)
            }
            start(overrideMinutes: command.minutes)

        case .stop:
            await stop(clearOutput: true)

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
            await reset()
        }
    }

    func start(overrideMinutes: Double? = nil) {
        guard !kind.requiresPro || canUseProFeatures() else {
            lastError = "\(effectiveTitle) requires Pro."
            return
        }

        if effectiveOutputStyle == 0, kind != .time, renderCustomOutput(for: 5).isEmpty {
            currentText = "Invalid time format. Use {0:hh:mm:ss}"
            lastError = currentText
            return
        }

        invalidateTimerEngine()
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
            pausedElapsed = overrideMinutes.map { max(0, $0) * 60 }
                ?? TimeInterval((minutes * 60) + seconds)
        }

        captureEndSoundSettings(startingElapsed: kind.isCountUp ? currentCountUpElapsed(at: now) : 0)

        activityToken = ProcessInfo.processInfo.beginActivity(
            options: [.userInitiated, .latencyCritical, .idleDisplaySleepDisabled],
            reason: "My Stream Timer is actively writing timer output."
        )
        launchTimerEngine()
    }

    func stop(clearOutput: Bool) async {
        let invalidatedGeneration = invalidateTimerEngine()
        await timerEngine.invalidate(upThrough: invalidatedGeneration)
        isRunning = false
        isPaused = false
        endActivity()

        if clearOutput {
            currentText = ""
            do {
                try await fileAccess.writeTimerOutput(text: "", fileName: fileName)
                lastError = nil
            } catch {
                lastError = error.localizedDescription
            }
        }
    }

    func pauseResume() {
        guard canPauseResume else { return }

        if !isPaused {
            if kind.isCountdown {
                pausedRemaining = max(0, endDate.timeIntervalSinceNow)
            } else if kind.isCountUp {
                pausedElapsed = max(0, Date().timeIntervalSince(startDate) + pausedElapsed)
            }
            isPaused = true
            invalidateTimerEngine()
        } else {
            isPaused = false
            if kind.isCountdown {
                endDate = Date().addingTimeInterval(pausedRemaining)
            } else if kind.isCountUp {
                startDate = Date()
                previousCountUpSoundElapsed = currentCountUpElapsed()
            }
            launchTimerEngine()
        }
    }

    func addMinute() {
        adjustBy(minutes: 1)
    }

    func reset() async {
        guard isRunning else { return }
        await stop(clearOutput: true)
        start()
    }

    func adjustBy(minutes delta: Double) {
        guard isRunning else { return }

        // The dates below aren't published, so tell observers that read displayInterval(at:).
        objectWillChange.send()

        if kind.isCountdown {
            if isPaused {
                pausedRemaining = max(0, pausedRemaining + (delta * 60))
            } else {
                endDate = endDate.addingTimeInterval(delta * 60)
                launchTimerEngine()
            }
        } else if kind.isCountUp {
            let previousElapsed = currentCountUpElapsed()
            pausedElapsed = max(0, pausedElapsed + (delta * 60))
            observeCountUpSoundCrossing(from: previousElapsed, to: currentCountUpElapsed())
            if !isPaused {
                launchTimerEngine()
            }
        }
    }

    /// The time this timer is showing right now, independent of its output format:
    /// time remaining for a countdown, time elapsed for a count-up.
    /// Nil when the timer is stopped, and always nil for the clock.
    func displayInterval(at date: Date = Date()) -> TimeInterval? {
        guard isRunning else { return nil }

        if kind.isCountdown {
            return isPaused ? pausedRemaining : max(0, endDate.timeIntervalSince(date))
        }
        if kind.isCountUp {
            return isPaused ? pausedElapsed : currentCountUpElapsed(at: date)
        }
        return nil
    }

    func refreshOutputDestination() {
        guard isRunning, !isPaused else { return }
        launchTimerEngine()
    }

    private func launchTimerEngine() {
        guard isRunning, !isPaused else { return }

        generation &+= 1
        let newGeneration = generation
        activeGeneration = newGeneration
        let configuration = TimerEngine.Configuration(
            generation: newGeneration,
            mode: kind.isCountdown ? .countdown : kind.isCountUp ? .countUp : .time,
            startDate: startDate,
            endDate: endDate,
            initialElapsed: pausedElapsed,
            output: output.isEmpty ? kind.defaultOutput : output,
            finishText: finishText,
            showAMPM: showAMPM,
            outputStyle: effectiveOutputStyle,
            destination: fileAccess.timerOutputDestination(fileName: fileName),
            countUpSoundTarget: kind.isCountUp && activeSoundEnabled && activeCountUpSoundTarget > 0 && !didPlayCountUpSound
                ? activeCountUpSoundTarget
                : nil
        )

        Task {
            await timerEngine.start(configuration)
        }
    }

    @discardableResult
    private func invalidateTimerEngine() -> UInt64 {
        generation &+= 1
        activeGeneration = nil
        let invalidatedGeneration = generation
        Task {
            await timerEngine.invalidate(upThrough: invalidatedGeneration)
        }
        return invalidatedGeneration
    }

    private func handleTimerEvent(_ event: TimerEngine.Event) {
        let eventGeneration: UInt64
        switch event {
        case let .rendered(generation, _, _),
             let .writeSucceeded(generation, _, _),
             let .writeFailed(generation, _),
             let .completed(generation):
            eventGeneration = generation
        }

        guard activeGeneration == eventGeneration else { return }

        switch event {
        case let .rendered(_, text, countUpElapsed):
            if currentText != text {
                currentText = text
            }
            if let countUpElapsed {
                observeCountUpSoundCrossing(from: previousCountUpSoundElapsed, to: countUpElapsed)
            }

        case let .writeSucceeded(_, refreshedBookmark, destination):
            fileAccess.applyRefreshedTimerBookmark(
                refreshedBookmark,
                destination: destination
            )
            lastError = nil

        case let .writeFailed(_, message):
            lastError = "Unable to save timer output: \(message)"

        case .completed:
            isRunning = false
            isPaused = false
            activeGeneration = nil
            endActivity()
            if activeSoundEnabled {
                playActiveEndSound()
            }
        }
    }

    static func shouldPlayCountUpSound(
        previous: TimeInterval,
        current: TimeInterval,
        target: TimeInterval
    ) -> Bool {
        target > 0 && previous < target && target <= current
    }

    private func saveCustomEndSound(_ customFile: CustomEndSoundFile) {
        customEndSoundBookmark = customFile.bookmark
        customEndSoundFileName = customFile.fileName
        endSound = .custom
        settingsStore.saveCustomEndSound(
            bookmark: customFile.bookmark,
            fileName: customFile.fileName,
            for: kind
        )
        persist(restartTimer: false)
    }

    private func updateCustomEndSoundBookmark(_ bookmark: Data) {
        customEndSoundBookmark = bookmark
        settingsStore.updateCustomEndSoundBookmark(bookmark, for: kind)
        persist(restartTimer: false)
    }

    private func captureEndSoundSettings(startingElapsed: TimeInterval) {
        activeSoundEnabled = kind != .time && beepAtZero
        activeEndSound = endSound
        activeCustomEndSoundBookmark = customEndSoundBookmark
        activeCountUpSoundTarget = TimeInterval((soundMinutes * 60) + min(59, max(0, soundSeconds)))
        previousCountUpSoundElapsed = startingElapsed
        didPlayCountUpSound = false
    }

    private func observeCountUpSoundCrossing(from previous: TimeInterval, to current: TimeInterval) {
        defer {
            previousCountUpSoundElapsed = current
        }
        guard kind.isCountUp,
              activeSoundEnabled,
              !didPlayCountUpSound,
              Self.shouldPlayCountUpSound(
                previous: previous,
                current: current,
                target: activeCountUpSoundTarget
              )
        else {
            return
        }

        didPlayCountUpSound = true
        playActiveEndSound()
    }

    private func currentCountUpElapsed(at date: Date = Date()) -> TimeInterval {
        guard kind.isCountUp else { return 0 }
        return max(0, date.timeIntervalSince(startDate) + pausedElapsed)
    }

    private func playActiveEndSound() {
        endSoundPlayer.playAtCompletion(
            sound: activeEndSound,
            customBookmark: activeCustomEndSoundBookmark,
            updateCustomBookmark: { [weak self] bookmark in
                self?.updateActiveCustomEndSoundBookmark(bookmark)
            }
        )
    }

    private func updateActiveCustomEndSoundBookmark(_ bookmark: Data) {
        activeCustomEndSoundBookmark = bookmark
        if activeEndSound == endSound {
            updateCustomEndSoundBookmark(bookmark)
        } else {
            settingsStore.updateCustomEndSoundBookmark(bookmark, for: kind)
        }
    }

    private func endActivity() {
        if let activityToken {
            ProcessInfo.processInfo.endActivity(activityToken)
            self.activityToken = nil
        }
    }

    private func renderCustomOutput(for interval: TimeInterval) -> String {
        let pattern = output.isEmpty ? kind.defaultOutput : output
        guard let regex = try? NSRegularExpression(pattern: #"\{0:([^}]*)\}"#) else {
            return pattern
        }

        let range = NSRange(pattern.startIndex..<pattern.endIndex, in: pattern)
        let matches = regex.matches(in: pattern, range: range)
        guard !matches.isEmpty else { return pattern }

        var rendered = pattern
        for match in matches.reversed() {
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
}
