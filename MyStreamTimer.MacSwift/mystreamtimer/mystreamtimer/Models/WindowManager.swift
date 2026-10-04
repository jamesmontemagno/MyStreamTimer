import AppKit

enum WindowManager {
    /// The app's own windows. Menu bar items are backed by untitled system windows that
    /// also appear in `NSApp.windows` and always report themselves as visible.
    @MainActor
    static var contentWindows: [NSWindow] {
        NSApp.windows.filter { $0.styleMask.contains(.titled) }
    }

    /// True while one of the app's windows is on screen or minimized to the Dock.
    @MainActor
    static var hasOpenContentWindow: Bool {
        contentWindows.contains { $0.isVisible || $0.isMiniaturized }
    }

    @MainActor
    static func applyStayOnTop(_ enabled: Bool) {
        for window in contentWindows {
            window.level = enabled ? .screenSaver : .normal
        }
    }
}
