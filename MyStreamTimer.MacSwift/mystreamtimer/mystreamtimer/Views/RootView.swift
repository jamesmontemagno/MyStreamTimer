import SwiftUI

// MARK: - Root

struct RootView: View {
    @EnvironmentObject private var appModel: AppModel

    var body: some View {
        NavigationSplitView {
            List(selection: $appModel.selectedItem) {
                Section("Countdowns") {
                    ForEach(appModel.countdownControllers) { controller in
                        TimerSidebarRow(controller: controller)
                            .tag(SidebarItem.timer(controller.kind))
                    }
                }

                Section("Count Up") {
                    ForEach(appModel.countUpControllers) { controller in
                        TimerSidebarRow(controller: controller)
                            .tag(SidebarItem.timer(controller.kind))
                    }
                }

                Section("Clock") {
                    TimerSidebarRow(controller: appModel.timeController)
                        .tag(SidebarItem.timer(appModel.timeController.kind))
                }

                Section {
                    Label("Automation", systemImage: "bolt.horizontal")
                        .tag(SidebarItem.automation)
                    Label("Settings", systemImage: "slider.horizontal.3")
                        .tag(SidebarItem.settings)
                }

                Section {
                    Label("Pro", systemImage: "sparkles")
                        .tag(SidebarItem.pro)
                    Label("About", systemImage: "info.circle")
                        .tag(SidebarItem.about)
                }
            }
            .navigationTitle("My Stream Timer")
            .listStyle(.sidebar)
        } detail: {
            ZStack {
                Color(nsColor: .windowBackgroundColor)
                    .ignoresSafeArea()

                detailContent
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            }
        }
        .navigationSplitViewStyle(.balanced)
        .sheet(isPresented: $appModel.showWelcomeBack) {
            WelcomeBackView()
                .environmentObject(appModel)
        }
        .alert(item: $appModel.alert) { alert in
            Alert(
                title: Text(alert.title),
                message: Text(alert.message),
                dismissButton: .default(Text("OK"))
            )
        }
    }

    @ViewBuilder
    private var detailContent: some View {
        switch appModel.selectedItem {
        case .timer(let kind):
            SingleTimerView(controller: appModel.controller(for: kind))
        case .automation:
            CommandsWorkspaceView()
        case .settings:
            SettingsWorkspaceView()
        case .pro:
            ProWorkspaceView()
        case .about:
            AboutWorkspaceView()
        }
    }
}

// MARK: - Sidebar row

struct TimerSidebarRow: View {
    @ObservedObject var controller: TimerController

    var body: some View {
        HStack {
            VStack(alignment: .leading, spacing: 2) {
                Text(controller.kind.title)
                Text(controller.fileName)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            Spacer()

            if controller.kind.requiresPro {
                StatusChip(title: "PRO", tint: .yellow)
            } else if controller.isRunning {
                StatusChip(
                    title: controller.isPaused ? "Paused" : "Live",
                    tint: controller.isPaused ? .orange : .green
                )
            }
        }
        .padding(.vertical, 2)
    }
}
