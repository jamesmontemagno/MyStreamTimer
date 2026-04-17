import SwiftUI

// MARK: - Automation

struct CommandsWorkspaceView: View {
    @EnvironmentObject private var appModel: AppModel

    private let commands: [CommandExample] = [
        CommandExample(
            title: "Countdown from minutes",
            command: "mystreamtimer://countdown/?mins=15",
            note: "Great for intros, break scenes, or timed announcements."
        ),
        CommandExample(
            title: "Countdown from seconds",
            command: "mystreamtimer://countdown/?secs=90",
            note: "Useful when Stream Deck passes shorter values."
        ),
        CommandExample(
            title: "Countdown to a clock time",
            command: "mystreamtimer://countdown/?to=15:30",
            note: "Schedule the end of a break or queue scene change."
        ),
        CommandExample(
            title: "Pause and resume",
            command: "mystreamtimer://countdown/?pause",
            note: "Swap to ?resume or ?reset for the rest of the controls."
        ),
        CommandExample(
            title: "Add time on the fly",
            command: "mystreamtimer://countdown/?addmins=1",
            note: "Works with addsecs, subtractmins, and subtractsecs too."
        ),
    ]

    var body: some View {
        WorkspaceContainer {
            WorkspaceHeader(
                eyebrow: "Automation",
                title: "Commands",
                subtitle: "Use mystreamtimer:// URLs in Stream Deck, Shortcuts, and scripts to control timers."
            )

            SectionCard(
                title: "Command builder",
                subtitle: "Generate and test automation links."
            ) {
                AutomationComposerView()
            }

            SectionCard(
                title: "Examples",
                subtitle: "Paste these into Stream Deck, shell scripts, Raycast, Alfred, or macOS Shortcuts."
            ) {
                VStack(alignment: .leading, spacing: 14) {
                    ForEach(commands) { example in
                        VStack(alignment: .leading, spacing: 6) {
                            HStack(alignment: .top) {
                                VStack(alignment: .leading, spacing: 4) {
                                    Text(example.title)
                                        .font(.subheadline.weight(.semibold))
                                    Text(example.command)
                                        .font(.system(.body, design: .monospaced))
                                        .textSelection(.enabled)
                                }

                                Spacer()

                                Button("Copy") {
                                    appModel.copyToClipboard(
                                        example.command,
                                        message: "Command copied to clipboard."
                                    )
                                }
                            }

                            Text(example.note)
                                .font(.caption)
                                .foregroundStyle(.secondary)
                        }

                        if example.id != commands.last?.id {
                            Divider()
                        }
                    }
                }
            }

            SectionCard(
                title: "Supported targets",
                subtitle: "Timer hosts and actions available via URL."
            ) {
                VStack(alignment: .leading, spacing: 8) {
                    BulletRow(text: "Hosts: countdown, countdown2, countdown3, countdown4, countup, countup2, time")
                    BulletRow(text: "Actions: start (mins/secs/to/topofhour), stop, pause, resume, reset, add, subtract")
                }
            }
        }
    }
}

struct AutomationComposerView: View {
    @EnvironmentObject private var appModel: AppModel

    @State private var selectedTimer: TimerKind = .countdown
    @State private var selectedAction: AutomationCommandPreset = .startMinutes
    @State private var commandValue = "15"

    private var generatedCommand: String {
        "mystreamtimer://\(selectedTimer.rawValue)/\(selectedAction.query(value: commandValue))"
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(spacing: 12) {
                Picker("Timer", selection: $selectedTimer) {
                    ForEach(TimerKind.allCases) { timer in
                        Text(timer.title).tag(timer)
                    }
                }

                Picker("Action", selection: $selectedAction) {
                    ForEach(AutomationCommandPreset.allCases) { action in
                        Text(action.title).tag(action)
                    }
                }
            }

            if selectedAction.requiresValue {
                TextField(selectedAction.valuePrompt, text: $commandValue)
            }

            Text(selectedAction.helpText)
                .font(.caption)
                .foregroundStyle(.secondary)

            Text(generatedCommand)
                .font(.system(.body, design: .monospaced))
                .textSelection(.enabled)
                .padding(10)
                .frame(maxWidth: .infinity, alignment: .leading)
                .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 12, style: .continuous))

            HStack {
                Button("Copy Command") {
                    appModel.copyToClipboard(
                        generatedCommand,
                        message: "Command copied to clipboard."
                    )
                }
                .buttonStyle(.borderedProminent)

                Button("Run in App") {
                    appModel.runAutomationCommand(generatedCommand)
                }
            }
        }
        .onChange(of: selectedAction) { _, newValue in
            commandValue = newValue.defaultValue
        }
    }
}

// MARK: - Automation model types

struct CommandExample: Identifiable {
    let id = UUID()
    let title: String
    let command: String
    let note: String
}

enum AutomationCommandPreset: String, CaseIterable, Identifiable {
    case startMinutes
    case startSeconds
    case startClockTime
    case topOfHour
    case pause
    case resume
    case reset
    case addMinutes
    case subtractMinutes
    case stop

    var id: String { rawValue }

    var title: String {
        switch self {
        case .startMinutes: return "Start from minutes"
        case .startSeconds: return "Start from seconds"
        case .startClockTime: return "Count down to time"
        case .topOfHour: return "Count down to top of hour"
        case .pause: return "Pause"
        case .resume: return "Resume"
        case .reset: return "Reset"
        case .addMinutes: return "Add minutes"
        case .subtractMinutes: return "Subtract minutes"
        case .stop: return "Stop"
        }
    }

    var defaultValue: String {
        switch self {
        case .startMinutes: return "15"
        case .startSeconds: return "90"
        case .startClockTime: return "15:30"
        case .topOfHour, .pause, .resume, .reset, .stop: return ""
        case .addMinutes, .subtractMinutes: return "1"
        }
    }

    var requiresValue: Bool {
        switch self {
        case .topOfHour, .pause, .resume, .reset, .stop: return false
        default: return true
        }
    }

    var valuePrompt: String {
        switch self {
        case .startClockTime: return "Clock time (e.g. 15:30)"
        case .startSeconds: return "Seconds"
        case .addMinutes, .subtractMinutes, .startMinutes: return "Minutes"
        case .topOfHour, .pause, .resume, .reset, .stop: return ""
        }
    }

    var helpText: String {
        switch self {
        case .startMinutes: return "Starts the selected timer from a number of minutes."
        case .startSeconds: return "Starts the selected timer from a raw seconds value."
        case .startClockTime: return "Counts down until the next time that clock value occurs."
        case .topOfHour: return "Convenient for scenes that always end on the hour."
        case .pause: return "Freezes the selected timer without clearing the current output."
        case .resume: return "Continues a previously paused timer."
        case .reset: return "Restarts the timer using the values configured in the workspace."
        case .addMinutes: return "Adds minutes while a timer is running."
        case .subtractMinutes: return "Subtracts minutes while a timer is running."
        case .stop: return "Stops the timer and clears the output file."
        }
    }

    func query(value: String) -> String {
        switch self {
        case .startMinutes: return "?mins=\(value)"
        case .startSeconds: return "?secs=\(value)"
        case .startClockTime: return "?to=\(value)"
        case .topOfHour: return "?topofhour"
        case .pause: return "?pause"
        case .resume: return "?resume"
        case .reset: return "?reset"
        case .addMinutes: return "?addmins=\(value)"
        case .subtractMinutes: return "?subtractmins=\(value)"
        case .stop: return "?stop"
        }
    }
}
