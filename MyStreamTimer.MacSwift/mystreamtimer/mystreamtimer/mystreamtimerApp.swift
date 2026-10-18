//
//  mystreamtimerApp.swift
//  mystreamtimer
//
//  Created by James Montemagno on 4/16/26.
//

import Foundation
import OSLog
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
    private var terminationTask: Task<Void, Never>?
    private static let logger = Logger(
        subsystem: Bundle.main.bundleIdentifier ?? "com.refractored.mystreamtimer",
        category: "Lifecycle"
    )

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

    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        guard let appModel = Self.appModel else { return .terminateNow }
        if terminationTask == nil {
            terminationTask = Task {
                _ = await Self.waitForShutdown(shutdown: { await appModel.shutdown() })
                sender.reply(toApplicationShouldTerminate: true)
            }
        }
        return .terminateLater
    }

    @MainActor
    static func waitForShutdown(
        timeoutNanoseconds: UInt64 = 5_000_000_000,
        shutdown: @escaping @MainActor @Sendable () async -> Void,
        reportTimeout: @escaping @MainActor @Sendable () -> Void = {
            AppDelegate.logger.error(
                "Timed out waiting for timer output writes; terminating without a confirmed shutdown drain."
            )
        }
    ) async -> Bool {
        // A task group would still wait for a cancelled, non-cooperative file write.
        await withCheckedContinuation { continuation in
            var finished = false
            var timeoutTask: Task<Void, Never>?
            let shutdownTask = Task { @MainActor in
                await shutdown()
                guard !finished else { return }
                finished = true
                timeoutTask?.cancel()
                timeoutTask = nil
                continuation.resume(returning: true)
            }
            timeoutTask = Task { @MainActor in
                do {
                    try await Task.sleep(nanoseconds: timeoutNanoseconds)
                } catch {
                    return
                }
                guard !finished else { return }
                finished = true
                shutdownTask.cancel()
                timeoutTask = nil
                reportTimeout()
                continuation.resume(returning: false)
            }
        }
    }
}
