import SwiftUI

// MARK: - Settings

struct SettingsWorkspaceView: View {
    @EnvironmentObject private var appModel: AppModel

    var body: some View {
        WorkspaceContainer {
            WorkspaceHeader(
                eyebrow: "Settings",
                title: "Output & behavior",
                subtitle: "Manage where timer files are saved and how the app behaves during streams."
            )

            SectionCard(
                title: "Output folder",
                subtitle: "OBS and browser sources read timer files from here."
            ) {
                VStack(alignment: .leading, spacing: 12) {
                    Text(appModel.settingsStore.directoryPath)
                        .font(.system(.body, design: .monospaced))
                        .textSelection(.enabled)

                    HStack {
                        Button {
                            appModel.chooseOutputFolder()
                        } label: {
                            Label("Choose Folder", systemImage: "folder.badge.plus")
                        }
                        .buttonStyle(AppActionButtonStyle(prominent: true))

                        Button {
                            appModel.openOutputFolder()
                        } label: {
                            Label("Open in Finder", systemImage: "folder")
                        }
                        .buttonStyle(AppActionButtonStyle())

                        Button {
                            appModel.validateOutputFolder()
                        } label: {
                            Label("Test Access", systemImage: "checkmark.shield")
                        }
                        .buttonStyle(AppActionButtonStyle())

                        Button {
                            appModel.resetOutputFolder()
                        } label: {
                            Label("Use Default", systemImage: "arrow.counterclockwise")
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

            SectionCard(
                title: "Timer output files",
                subtitle: "Point your stream scenes at these filenames."
            ) {
                LazyVGrid(columns: [GridItem(.adaptive(minimum: 170), spacing: 10)], spacing: 10) {
                    ForEach(appModel.allControllers) { controller in
                        Label(controller.fileName, systemImage: controller.effectiveSystemImage)
                            .font(.subheadline)
                            .padding(.horizontal, 10)
                            .padding(.vertical, 8)
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 10, style: .continuous))
                    }
                }
            }

            SectionCard(
                title: "Timer appearance",
                subtitle: "Choose the name and icon shown for each timer."
            ) {
                VStack(spacing: 12) {
                    ForEach(appModel.allControllers) { controller in
                        TimerAppearanceRow(controller: controller)
                    }
                }
            }

            SectionCard(title: "Appearance", subtitle: "Choose how the app looks.") {
                Picker("Theme", selection: Binding(
                    get: { appModel.settingsStore.theme },
                    set: { appModel.settingsStore.theme = $0 }
                )) {
                    ForEach(AppTheme.allCases) { theme in
                        Text(theme.displayName).tag(theme)
                    }
                }
                .pickerStyle(.segmented)
                .fixedSize()
            }

            SectionCard(title: "Window", subtitle: "Streaming quality-of-life options.") {
                VStack(alignment: .leading, spacing: 10) {
                    LeadingToggleRow(
                        isOn: Binding(
                            get: { appModel.settingsStore.stayOnTop },
                            set: { appModel.settingsStore.stayOnTop = $0 }
                        )
                    ) {
                        VStack(alignment: .leading, spacing: 2) {
                            Text("Stay on top of other windows")
                            Text("Keep the main timer window above other apps.")
                                .font(.caption)
                                .foregroundStyle(.secondary)
                        }
                    }

                }
            }

            PopOutAppearanceCard(
                settingsStore: appModel.settingsStore,
                isPro: appModel.purchaseManager.isPro,
                goToPro: { appModel.selectedItem = .pro }
            )
        }
    }
}

private struct TimerAppearanceRow: View {
    @ObservedObject var controller: TimerController

    var body: some View {
        HStack(spacing: 12) {
            Label(controller.kind.title, systemImage: controller.effectiveSystemImage)
                .frame(width: 140, alignment: .leading)

            TextField(
                controller.kind.title,
                text: Binding(
                    get: { controller.displayName },
                    set: {
                        controller.displayName = $0
                        controller.persist(restartTimer: false)
                    }
                )
            )
            .textFieldStyle(.roundedBorder)

            Picker(
                "Icon",
                selection: Binding(
                    get: { controller.iconGlyph },
                    set: {
                        controller.iconGlyph = $0
                        controller.persist(restartTimer: false)
                    }
                )
            ) {
                Label("Default", systemImage: controller.kind.systemImage)
                    .tag("")
                Divider()
                ForEach(TimerKind.iconChoices) { choice in
                    Label(choice.label, systemImage: choice.systemName)
                        .tag(choice.systemName)
                }
            }
            .pickerStyle(.menu)
            .frame(width: 150)
        }
    }
}

// MARK: - Pop-out appearance (Pro)

struct PopOutAppearanceCard: View {
    @ObservedObject var settingsStore: LegacySettingsStore
    let isPro: Bool
    let goToPro: () -> Void

    var body: some View {
        if isPro {
            SectionCard(
                title: "Pop-out preview appearance",
                subtitle: "Customize the look of pop-out timer windows."
            ) {
                VStack(alignment: .leading, spacing: 14) {
                    VStack(alignment: .leading, spacing: 4) {
                        Text("Font size: \(Int(settingsStore.popOutFontSize)) pt")
                            .font(.caption.weight(.medium))
                            .foregroundStyle(.secondary)

                        Slider(
                            value: $settingsStore.popOutFontSize,
                            in: 16...120,
                            step: 2
                        )
                    }

                    Picker("Font", selection: $settingsStore.popOutFontFamily) {
                        Text("System (Rounded)").tag("")
                        Divider()
                        ForEach(LegacySettingsStore.availableFontFamilies, id: \.self) { family in
                            Text(family)
                                .font(.custom(family, size: 13))
                                .tag(family)
                        }
                    }
                    .pickerStyle(.menu)

                    HStack(spacing: 24) {
                        ColorPicker(
                            "Text color",
                            selection: Binding(
                                get: { Color(hex: settingsStore.popOutTextColorHex) ?? .white },
                                set: { settingsStore.popOutTextColorHex = $0.hexString }
                            ),
                            supportsOpacity: false
                        )

                        ColorPicker(
                            "Background color",
                            selection: Binding(
                                get: { Color(hex: settingsStore.popOutBackgroundColorHex) ?? .black },
                                set: { settingsStore.popOutBackgroundColorHex = $0.hexString }
                            ),
                            supportsOpacity: false
                        )
                    }

                    Divider()

                    VStack(alignment: .leading, spacing: 4) {
                        Text("Preview")
                            .font(.caption.weight(.medium))
                            .foregroundStyle(.secondary)

                        ZStack {
                            RoundedRectangle(cornerRadius: 10, style: .continuous)
                                .fill(Color(hex: settingsStore.popOutBackgroundColorHex) ?? .black)

                            Text("12:34")
                                .font(settingsStore.popOutFont)
                                .monospacedDigit()
                                .foregroundStyle(Color(hex: settingsStore.popOutTextColorHex) ?? .white)
                                .padding(20)
                        }
                        .frame(maxWidth: .infinity, minHeight: max(80, settingsStore.popOutFontSize + 40))
                    }
                }
            }
        } else {
            SectionCard(
                title: "Pop-out preview appearance",
                subtitle: "Customize font size, text color, and background color of pop-out windows."
            ) {
                HStack(spacing: 16) {
                    Image(systemName: "lock.fill")
                        .font(.title)
                        .foregroundStyle(.secondary)

                    VStack(alignment: .leading, spacing: 4) {
                        Text("Upgrade to Pro to customize pop-out appearance.")
                            .foregroundStyle(.secondary)

                        Button {
                            goToPro()
                        } label: {
                            Label("Go to Pro", systemImage: "sparkles")
                        }
                        .buttonStyle(AppActionButtonStyle(prominent: true))
                    }
                }
            }
        }
    }
}
