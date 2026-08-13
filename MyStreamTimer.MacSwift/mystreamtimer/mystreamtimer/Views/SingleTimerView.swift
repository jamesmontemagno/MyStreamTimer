import SwiftUI

// MARK: - Single timer detail

struct SingleTimerView: View {
    @EnvironmentObject private var appModel: AppModel
    @ObservedObject var controller: TimerController
    @Environment(\.openWindow) private var openWindow

    private var isLocked: Bool {
        controller.kind.requiresPro && !appModel.purchaseManager.isPro
    }

    var body: some View {
        WorkspaceContainer {
            timerHeader

            if isLocked {
                proLockedCard
            } else {
                transportBar
                if controller.kind != .time {
                    timeInputCard
                }
                formatCard
                behaviorCard
                outputCard
            }
        }
    }

    // MARK: - Live preview header

    private var timerHeader: some View {
        VStack(spacing: 0) {
            HStack(alignment: .firstTextBaseline) {
                Label(controller.kind.title, systemImage: controller.kind.systemImage)
                    .font(.title2.weight(.semibold))

                Spacer()

                Button {
                    if appModel.purchaseManager.isPro {
                        openWindow(value: controller.kind)
                    } else {
                        appModel.showAlert(
                            title: "Pro Feature",
                            message: "Pop-out preview windows are a Pro feature. Upgrade to Pro to pop out timers, customize font size, text color, and background color."
                        )
                        appModel.selectedItem = .pro
                    }
                } label: {
                    Image(systemName: "arrow.up.right.square")
                }
                .buttonStyle(.borderless)
                .help("Pop out preview")

                StatusChip(title: controller.statusLabel, tint: controller.statusTint)

                if isLocked {
                    StatusChip(title: "PRO", tint: .yellow)
                }
            }
            .padding(.bottom, 12)

            ZStack {
                RoundedRectangle(cornerRadius: 20, style: .continuous)
                    .fill(
                        controller.isRunning && !controller.isPaused
                            ? Color.accentColor.opacity(0.08)
                            : Color(nsColor: .controlBackgroundColor)
                    )
                    .overlay(
                        RoundedRectangle(cornerRadius: 20, style: .continuous)
                            .strokeBorder(
                                controller.isRunning && !controller.isPaused
                                    ? Color.accentColor.opacity(0.3)
                                    : Color.gray.opacity(0.2),
                                lineWidth: 1
                            )
                    )

                VStack(spacing: 6) {
                    Text(controller.previewDisplayText)
                        .font(.system(size: 56, weight: .bold, design: .rounded))
                        .monospacedDigit()
                        .foregroundStyle(
                            controller.isRunning && !controller.isPaused
                                ? Color.primary
                                : Color.primary.opacity(0.72)
                        )
                        .contentTransition(.numericText())
                        .animation(.easeInOut(duration: 0.15), value: controller.previewDisplayText)

                    if controller.isRunning {
                        Text(controller.isPaused ? "Paused" : "Writing to \(controller.fileName)")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                }
                .padding(.vertical, 24)
            }

            if let lastError = controller.lastError {
                HStack(spacing: 6) {
                    Image(systemName: "exclamationmark.triangle.fill")
                        .foregroundStyle(.red)
                    Text(lastError)
                        .foregroundStyle(.red)
                }
                .font(.caption)
                .padding(.top, 8)
            }
        }
    }

    // MARK: - Transport controls

    private var transportBar: some View {
        HStack(spacing: 10) {
            Button {
                Task {
                    if controller.isRunning {
                        await controller.stop(clearOutput: true)
                    } else {
                        controller.start()
                    }
                }
            } label: {
                Label(
                    controller.isRunning ? "Stop" : "Start",
                    systemImage: controller.isRunning ? "stop.fill" : "play.fill"
                )
                .frame(minWidth: 80)
            }
            .buttonStyle(
                AppActionButtonStyle(
                    prominent: true,
                    tint: controller.isRunning ? .red : .accentColor
                )
            )

            if controller.kind != .time {
                Button {
                    controller.pauseResume()
                } label: {
                    Label(
                        controller.isPaused ? "Resume" : "Pause",
                        systemImage: controller.isPaused ? "play.fill" : "pause.fill"
                    )
                }
                .buttonStyle(AppActionButtonStyle())
                .disabled(!controller.canPauseResume)

                Spacer()

                HStack(spacing: 8) {
                    Button {
                        controller.adjustBy(minutes: -1)
                    } label: {
                        Image(systemName: "minus")
                            .frame(width: 18, height: 18)
                    }
                    .buttonStyle(AppActionButtonStyle())
                    .frame(width: 44)
                    .disabled(!controller.isRunning)
                    .help("Subtract 1 minute")

                    Text("1 min")
                        .font(.caption.weight(.medium))
                        .foregroundStyle(.secondary)

                    Button {
                        controller.addMinute()
                    } label: {
                        Image(systemName: "plus")
                            .frame(width: 18, height: 18)
                    }
                    .buttonStyle(AppActionButtonStyle())
                    .frame(width: 44)
                    .disabled(!controller.isRunning)
                    .help("Add 1 minute")
                }

                Button {
                    Task {
                        await controller.reset()
                    }
                } label: {
                    Label("Reset", systemImage: "arrow.counterclockwise")
                }
                .buttonStyle(AppActionButtonStyle())
                .disabled(!controller.isRunning)
            }
        }
        .padding(14)
        .background(
            RoundedRectangle(cornerRadius: 14, style: .continuous)
                .fill(Color(nsColor: .controlBackgroundColor))
        )
        .overlay(
            RoundedRectangle(cornerRadius: 14, style: .continuous)
                .strokeBorder(.quaternary, lineWidth: 1)
        )
    }

    // MARK: - Pro locked

    private var proLockedCard: some View {
        SectionCard(
            title: "Pro feature",
            subtitle: "\(controller.kind.title) requires a Pro unlock."
        ) {
            HStack(spacing: 16) {
                Image(systemName: "lock.fill")
                    .font(.title)
                    .foregroundStyle(.secondary)

                VStack(alignment: .leading, spacing: 4) {
                    Text("Upgrade to Pro to unlock this timer and all advanced features.")
                        .foregroundStyle(.secondary)

                    Button {
                        appModel.selectedItem = .pro
                    } label: {
                        Label("Go to Pro", systemImage: "sparkles")
                    }
                    .buttonStyle(AppActionButtonStyle(prominent: true))
                }
            }
        }
    }

    // MARK: - Time input

    private var timeInputCard: some View {
        SectionCard(
            title: controller.kind.isCountUp ? "Start at" : "Duration",
            subtitle: controller.kind.isCountUp
                ? "Optionally begin the count up from a specific elapsed time."
                : "Set how long the timer runs."
        ) {
            if controller.kind.isCountdown {
                countdownInputSection
            } else {
                durationFields
            }
        }
    }

    private var clockFormatGrid: some View {
        VStack(alignment: .leading, spacing: 14) {
            LazyVGrid(columns: [GridItem(.adaptive(minimum: 160), spacing: 10)], spacing: 10) {
                ForEach(Array(controller.kind.outputStyleOptions.enumerated()), id: \.offset) { index, title in
                    OptionTile(
                        title: title,
                        isSelected: controller.outputStyle == index
                    ) {
                        controller.outputStyle = index
                        controller.persist()
                    }
                }
            }

            LeadingToggleRow(
                isOn: Binding(
                    get: { controller.showAMPM },
                    set: {
                        controller.showAMPM = $0
                        controller.persist()
                    }
                )
            ) {
                Label("Show AM/PM suffix", systemImage: "clock")
            }
        }
    }

    private var countdownInputSection: some View {
        VStack(alignment: .leading, spacing: 14) {
            Picker("Mode", selection: Binding(
                get: { controller.useMinutes },
                set: {
                    controller.useMinutes = $0
                    controller.persist()
                })
            ) {
                Label("Duration", systemImage: "timer").tag(true)
                Label("Clock Time", systemImage: "clock").tag(false)
            }
            .pickerStyle(.segmented)

            if controller.useMinutes {
                durationFields
            } else {
                HStack(spacing: 14) {
                    Label("Finish at", systemImage: "clock.badge.checkmark")
                        .font(.body.weight(.semibold))

                    DatePicker(
                        "",
                        selection: Binding(
                            get: { controller.finishAt },
                            set: {
                                controller.finishAt = $0
                                controller.persist()
                            }),
                        displayedComponents: [.hourAndMinute]
                    )
                    .labelsHidden()
                    .datePickerStyle(.stepperField)
                    .controlSize(.large)

                    Spacer()
                }
                .padding(12)
                .background(
                    RoundedRectangle(cornerRadius: 12, style: .continuous)
                        .fill(Color(nsColor: .controlBackgroundColor))
                )
                .overlay {
                    RoundedRectangle(cornerRadius: 12, style: .continuous)
                        .strokeBorder(Color(nsColor: .separatorColor).opacity(0.7))
                }

                Text("Timer will count down until this time. If the time has passed today, it targets tomorrow.")
                    .font(.caption)
                    .foregroundStyle(.tertiary)
            }
        }
    }

    private var durationFields: some View {
        HStack(spacing: 16) {
            NumericTimeField(
                title: "Minutes",
                value: Binding(
                        get: { controller.minutes },
                        set: {
                            controller.minutes = $0
                            controller.persist()
                        }
                    ),
                range: 0...100_000
            )

            NumericTimeField(
                title: "Seconds",
                value: Binding(
                        get: { controller.seconds },
                        set: {
                            controller.seconds = $0
                            controller.persist()
                        }
                    ),
                range: 0...59
            )

            Spacer()
        }
    }

    // MARK: - Output format

    private var formatCard: some View {
        SectionCard(title: "Format", subtitle: "How the timer value appears in the text file.") {
            if controller.kind == .time {
                clockFormatGrid
            } else {
                VStack(alignment: .leading, spacing: 12) {
                    LazyVGrid(columns: [GridItem(.adaptive(minimum: 140), spacing: 10)], spacing: 10) {
                        ForEach(Array(controller.kind.outputStyleOptions.enumerated()), id: \.offset) { index, title in
                            let isProFormatLocked = index > 0 && !appModel.purchaseManager.isPro
                            OptionTile(
                                title: title,
                                isSelected: controller.effectiveOutputStyle == index,
                                isLocked: isProFormatLocked
                            ) {
                                guard !isProFormatLocked else {
                                    appModel.showAlert(
                                        title: "Pro Feature",
                                        message: "\(title) is available with My Stream Timer Pro."
                                    )
                                    appModel.selectedItem = .pro
                                    return
                                }
                                controller.outputStyle = index
                                controller.persist()
                            }
                        }
                    }

                    if controller.effectiveOutputStyle == 0 {
                        VStack(alignment: .leading, spacing: 4) {
                            Text("Template")
                                .font(.caption.weight(.medium))
                                .foregroundStyle(.secondary)

                            TextField("{0:hh:mm:ss}", text: Binding(
                                get: { controller.output },
                                set: {
                                    controller.output = $0
                                    controller.persist()
                                })
                            )
                            .textFieldStyle(.roundedBorder)
                            .font(.system(.body, design: .monospaced))

                            Text("Use {0:hh:mm:ss} format. Tokens: d, dd, h, hh, m, mm, s, ss.")
                                .font(.caption)
                                .foregroundStyle(.tertiary)
                        }
                    }

                    if controller.kind.isCountdown {
                        VStack(alignment: .leading, spacing: 4) {
                            Text("Finish text")
                                .font(.caption.weight(.medium))
                                .foregroundStyle(.secondary)

                            TextField("Starting Soon!", text: Binding(
                                get: { controller.finishText },
                                set: {
                                    controller.finishText = $0
                                    controller.persist()
                                })
                            )
                            .textFieldStyle(.roundedBorder)

                            Text("Written to the file when the countdown reaches zero.")
                                .font(.caption)
                                .foregroundStyle(.tertiary)
                        }
                    }
                }
            }
        }
    }

    // MARK: - Behavior toggles

    private var behaviorCard: some View {
        SectionCard(title: "Behavior", subtitle: "Automation and alerts.") {
            VStack(alignment: .leading, spacing: 10) {
                LeadingToggleRow(
                    isOn: Binding(
                        get: { controller.autoStart },
                        set: {
                            controller.autoStart = $0
                            controller.persist()
                        }
                    )
                ) {
                    VStack(alignment: .leading, spacing: 2) {
                        Text("Auto start on launch")
                        Text("Timer starts automatically when the app opens.")
                            .font(.caption)
                            .foregroundStyle(.tertiary)
                    }
                }

                if controller.kind.isCountdown {
                    Divider()

                    LeadingToggleRow(
                        isOn: Binding(
                            get: { controller.beepAtZero },
                            set: {
                                controller.beepAtZero = $0
                                controller.persist()
                            }
                        )
                    ) {
                        VStack(alignment: .leading, spacing: 2) {
                            Text("Beep at zero")
                            Text("Play the system alert sound when the countdown finishes.")
                                .font(.caption)
                                .foregroundStyle(.tertiary)
                        }
                    }
                }
            }
        }
    }

    // MARK: - Output file

    private var outputCard: some View {
        SectionCard(title: "Output file", subtitle: "Point OBS to this file for your stream overlay.") {
            VStack(alignment: .leading, spacing: 10) {
                HStack(spacing: 8) {
                    Text("File name")
                        .font(.caption.weight(.medium))
                        .foregroundStyle(.secondary)

                    TextField("countdown.txt", text: Binding(
                        get: { controller.fileName },
                        set: {
                            controller.fileName = $0
                            controller.persist()
                        })
                    )
                    .textFieldStyle(.roundedBorder)
                    .font(.system(.body, design: .monospaced))
                }

                HStack(spacing: 6) {
                    Image(systemName: "folder")
                        .foregroundStyle(.secondary)

                    Text(appModel.settingsStore.directoryPath)
                        .font(.system(.caption, design: .monospaced))
                        .foregroundStyle(.secondary)
                        .textSelection(.enabled)
                        .lineLimit(1)
                        .truncationMode(.middle)

                    Spacer()

                    Button {
                        appModel.openOutputFolder()
                    } label: {
                        Label("Open", systemImage: "folder")
                    }
                    .buttonStyle(AppActionButtonStyle())

                    Button {
                        appModel.copyOutputFolder()
                    } label: {
                        Label("Copy Path", systemImage: "doc.on.doc")
                    }
                    .buttonStyle(AppActionButtonStyle())
                }
            }
        }
    }
}

// MARK: - Option tile

struct OptionTile: View {
    let title: String
    let isSelected: Bool
    var isLocked = false
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            HStack(spacing: 6) {
                Text(title)
                    .font(.subheadline.weight(isSelected ? .semibold : .regular))
                if isLocked {
                    Image(systemName: "lock.fill")
                        .font(.caption)
                }
            }
                .frame(maxWidth: .infinity)
                .padding(.vertical, 10)
                .padding(.horizontal, 12)
                .background(
                    RoundedRectangle(cornerRadius: 10, style: .continuous)
                        .fill(isSelected ? Color.accentColor.opacity(0.18) : Color(nsColor: .controlBackgroundColor))
                )
                .overlay(
                    RoundedRectangle(cornerRadius: 10, style: .continuous)
                        .strokeBorder(
                            isSelected ? Color.accentColor : Color(nsColor: .separatorColor).opacity(0.75),
                            lineWidth: 1
                        )
                )
        }
        .buttonStyle(.plain)
        .foregroundStyle(isLocked ? Color.secondary : (isSelected ? Color.accentColor : Color.primary))
    }
}

private struct NumericTimeField: View {
    let title: String
    @Binding var value: Int
    let range: ClosedRange<Int>

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(title)
                .font(.caption.weight(.semibold))
                .foregroundStyle(.secondary)

            HStack(spacing: 8) {
                Button {
                    value = max(range.lowerBound, value - 1)
                } label: {
                    Image(systemName: "minus")
                        .frame(width: 18, height: 18)
                }
                .buttonStyle(AppActionButtonStyle())
                .frame(width: 44)
                .disabled(value <= range.lowerBound)

                TextField("0", value: $value, format: .number)
                    .textFieldStyle(.roundedBorder)
                    .multilineTextAlignment(.center)
                    .font(.title3.monospacedDigit())
                    .frame(width: 88)
                    .onChange(of: value) { _, newValue in
                        value = min(range.upperBound, max(range.lowerBound, newValue))
                    }

                Button {
                    value = min(range.upperBound, value + 1)
                } label: {
                    Image(systemName: "plus")
                        .frame(width: 18, height: 18)
                }
                .buttonStyle(AppActionButtonStyle())
                .frame(width: 44)
                .disabled(value >= range.upperBound)
            }
        }
    }
}
