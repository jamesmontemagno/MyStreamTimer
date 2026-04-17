import AppKit

enum WindowManager {
    @MainActor
    static func applyStayOnTop(_ enabled: Bool) {
        for window in NSApp.windows {
            window.level = enabled ? .screenSaver : .normal
        }
    }
}
