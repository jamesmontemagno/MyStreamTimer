import AppKit
import Combine
import StoreKit
import SwiftUI

enum SidebarItem: Hashable {
    case timer(TimerKind)
    case automation
    case settings
    case pro
    case about
}

struct AppAlert: Identifiable {
    let id = UUID()
    let title: String
    let message: String
}

@MainActor
final class AppModel: ObservableObject {
    @Published var selectedItem: SidebarItem = .timer(.countdown)
    @Published var alert: AppAlert?
    @Published var showWelcomeBack = false

    let settingsStore: LegacySettingsStore
    let fileAccess: BookmarkFileAccess
    let purchaseManager: PurchaseManager

    let countdownControllers: [TimerController]
    let countUpControllers: [TimerController]
    let timeController: TimerController

    private lazy var controllerLookup: [TimerKind: TimerController] = {
        var lookup = [TimerKind: TimerController]()
        (countdownControllers + countUpControllers + [timeController]).forEach {
            lookup[$0.kind] = $0
        }
        return lookup
    }()

    private var hasStarted = false

    var allControllers: [TimerController] {
        countdownControllers + countUpControllers + [timeController]
    }

    var activeTimerCount: Int {
        allControllers.filter(\.isRunning).count
    }

    var lockedTimerCount: Int {
        allControllers.filter { $0.kind.requiresPro && !purchaseManager.isPro }.count
    }

    var outputFolderName: String {
        let lastComponent = URL(fileURLWithPath: settingsStore.directoryPath).lastPathComponent
        return lastComponent.isEmpty ? settingsStore.directoryPath : lastComponent
    }

    func controller(for kind: TimerKind) -> TimerController {
        controllerLookup[kind]!
    }

    init() {
        let settingsStore = LegacySettingsStore()
        self.settingsStore = settingsStore

        let fileAccess = BookmarkFileAccess(settingsStore: settingsStore)
        self.fileAccess = fileAccess

        self.purchaseManager = PurchaseManager(settingsStore: settingsStore)

        self.countdownControllers = [
            TimerController(kind: .countdown, settingsStore: settingsStore, fileAccess: fileAccess),
            TimerController(kind: .countdown2, settingsStore: settingsStore, fileAccess: fileAccess),
            TimerController(kind: .countdown3, settingsStore: settingsStore, fileAccess: fileAccess),
            TimerController(kind: .countdown4, settingsStore: settingsStore, fileAccess: fileAccess),
        ]

        self.countUpControllers = [
            TimerController(kind: .countup, settingsStore: settingsStore, fileAccess: fileAccess),
            TimerController(kind: .countup2, settingsStore: settingsStore, fileAccess: fileAccess),
        ]

        self.timeController = TimerController(kind: .time, settingsStore: settingsStore, fileAccess: fileAccess)
    }

    func startup() async {
        guard !hasStarted else { return }
        hasStarted = true

        WindowManager.applyStayOnTop(settingsStore.stayOnTop)
        settingsStore.timesUsed += 1

        if settingsStore.timesUsed == 10 {
            SKStoreReviewController.requestReview()
        }

        if settingsStore.timesUsed > 1 && !settingsStore.hasSeenWelcomeBack {
            settingsStore.hasSeenWelcomeBack = true
            showWelcomeBack = true
        }

        await purchaseManager.start()
    }

    func showAlert(title: String, message: String) {
        alert = AppAlert(title: title, message: message)
    }

    func openExternalURL(_ urlString: String) {
        let trimmed = urlString.trimmingCharacters(in: .whitespacesAndNewlines)
        guard let url = URL(string: trimmed) else {
            showAlert(title: "Invalid Link", message: "That link couldn't be opened.")
            return
        }
        NSWorkspace.shared.open(url)
    }

    func runAutomationCommand(_ commandText: String) {
        let trimmed = commandText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard let url = URL(string: trimmed) else {
            showAlert(title: "Invalid Command", message: "Please enter a valid mystreamtimer:// URL.")
            return
        }

        handleIncomingURL(url)
    }

    func copyToClipboard(_ text: String, message: String? = nil) {
        let pasteboard = NSPasteboard.general
        pasteboard.clearContents()
        pasteboard.setString(text, forType: .string)

        if let message {
            showAlert(title: "Copied", message: message)
        }
    }

    func copyOutputFolder() {
        copyToClipboard(
            settingsStore.directoryPath,
            message: "The output folder path has been copied to your clipboard."
        )
    }

    func openOutputFolder() {
        let url = URL(fileURLWithPath: settingsStore.directoryPath, isDirectory: true)
        NSWorkspace.shared.open(url)
    }

    func chooseOutputFolder() {
        do {
            if let selectedPath = try fileAccess.chooseDirectory() {
                settingsStore.updateDirectoryPath(selectedPath)
                showAlert(
                    title: "Folder Updated",
                    message: "My Stream Timer will now save output files to the selected location."
                )
            }
        } catch {
            showAlert(title: "Folder Access", message: error.localizedDescription)
        }
    }

    func resetOutputFolder() {
        fileAccess.resetToDefaultDirectory()
        showAlert(
            title: "Folder Reset",
            message: "The output folder has been reset to the default app Documents location."
        )
    }

    func validateOutputFolder() {
        do {
            try fileAccess.validateDirectory(path: settingsStore.directoryPath)
            showAlert(
                title: "Success",
                message: "This directory is writable and ready to use for timer outputs."
            )
        } catch {
            showAlert(title: "Folder Validation", message: error.localizedDescription)
        }
    }

    func handleIncomingURL(_ url: URL) {
        guard let command = URLCommand(url: url) else {
            showAlert(title: "Unsupported Command", message: "That URL isn't recognized by My Stream Timer.")
            return
        }

        if command.kind.requiresPro && !purchaseManager.isPro {
            selectedItem = .pro
            showAlert(
                title: "Pro Feature",
                message: "\(command.kind.title) requires Pro."
            )
            return
        }

        selectedItem = .timer(command.kind)
        controllerLookup[command.kind]?.apply(command)

        if settingsStore.hideOnAutomation {
            NSApp.hide(nil)
        }
    }

    func purchase(productID: String) async {
        let message = await purchaseManager.purchase(productID: productID)
        showAlert(title: "Purchases", message: message)
    }

    func restorePurchases() async {
        let message = await purchaseManager.restorePurchases()
        showAlert(title: "Restore Purchases", message: message)
    }
}
