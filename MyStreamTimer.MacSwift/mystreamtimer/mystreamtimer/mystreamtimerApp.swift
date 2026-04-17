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
    @StateObject private var appModel = AppModel()

    var body: some Scene {
        Window("My Stream Timer", id: "main") {
            ContentView()
                .environmentObject(appModel)
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

        Settings {
            SettingsWorkspaceView()
                .environmentObject(appModel)
                .frame(minWidth: 680, minHeight: 420)
        }
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        if !flag {
            // Reactivate existing window instead of opening a new one
            sender.windows.first?.makeKeyAndOrderFront(self)
        }
        return true
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        false
    }
}
