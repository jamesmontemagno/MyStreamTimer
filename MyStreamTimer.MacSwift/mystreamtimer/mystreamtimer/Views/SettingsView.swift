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
                        Button("Choose Folder") {
                            appModel.chooseOutputFolder()
                        }
                        .buttonStyle(.borderedProminent)

                        Button("Open in Finder") {
                            appModel.openOutputFolder()
                        }

                        Button("Test Access") {
                            appModel.validateOutputFolder()
                        }

                        Button("Use Default Folder") {
                            appModel.resetOutputFolder()
                        }

                        Button("Copy Path") {
                            appModel.copyOutputFolder()
                        }
                    }
                }
            }

            SectionCard(
                title: "Timer output files",
                subtitle: "Point your stream scenes at these filenames."
            ) {
                LazyVGrid(columns: [GridItem(.adaptive(minimum: 170), spacing: 10)], spacing: 10) {
                    ForEach(appModel.allControllers) { controller in
                        Label(controller.fileName, systemImage: controller.kind.systemImage)
                            .font(.subheadline)
                            .padding(.horizontal, 10)
                            .padding(.vertical, 8)
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 10, style: .continuous))
                    }
                }
            }

            SectionCard(title: "Window", subtitle: "Streaming quality-of-life options.") {
                Toggle("Stay on top of other windows", isOn: Binding(
                    get: { appModel.settingsStore.stayOnTop },
                    set: { appModel.settingsStore.stayOnTop = $0 }
                ))
            }
        }
    }
}
