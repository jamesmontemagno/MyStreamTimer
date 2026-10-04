//
//  mystreamtimerApp.swift
//  mystreamtimer
//
//  Created by James Montemagno on 4/16/26.
//

import SwiftUI

@main
struct mystreamtimerApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @Environment(\.openWindow) private var openWindow
    @StateObject private var appModel: AppModel

    init() {
        let appModel = AppModel()
        _appModel = StateObject(wrappedValue: appModel)
        AppDelegate.appModel = appModel
    }

    var body: some Scene {
        Window("My Stream Timer", id: "main") {
            ContentView()
                .environmentObject(appModel)
                .preferredColorScheme(appModel.settingsStore.theme.colorScheme)
                .frame(minWidth: 520, minHeight: 400)
                .task {
                    await appModel.startup()
                }
                .onOpenURL { url in
                    appModel.handleIncomingURL(url)
                }
        }
        .defaultPosition(.center)
        .commands {
            // Remove "New Window" from the File menu
            CommandGroup(replacing: .newItem) { }
        }
        .onChange(of: appModel.mainWindowRequest) {
            // Works whether the window is closed, minimized, or was never created.
            openWindow(id: "main")
            NSApp.activate()
        }

        WindowGroup("Timer Preview", for: TimerKind.self) { $kind in
            if let kind {
                TimerMiniView(kind: kind)
                    .environmentObject(appModel)
                    .preferredColorScheme(appModel.settingsStore.theme.colorScheme)
            }
        }
        .windowStyle(.hiddenTitleBar)
        .windowResizability(.contentSize)
        .defaultSize(width: 400, height: 160)
        .defaultPosition(.topTrailing)

        Settings {
            SettingsWorkspaceView()
                .environmentObject(appModel)
                .preferredColorScheme(appModel.settingsStore.theme.colorScheme)
                .frame(minWidth: 680, minHeight: 420)
        }
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    static weak var appModel: AppModel?

    func applicationDidFinishLaunching(_ notification: Notification) {
        // Start here rather than from the main window, which isn't always shown at launch.
        Task {
            await Self.appModel?.startup()
        }
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        guard !flag, let appModel = Self.appModel else { return true }

        // Nothing is on screen, so bring back the main window. Menu bar items also have
        // windows in `sender.windows`, so the first one there is not necessarily ours.
        appModel.showMainWindow()
        return false
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        // Timers shown in the menu bar keep the app, and its timers, running without a window.
        !(Self.appModel?.menuBarController.hasVisibleItems ?? false)
    }
}
