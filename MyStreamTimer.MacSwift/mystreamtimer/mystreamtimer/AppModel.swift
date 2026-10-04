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
    /// Changes whenever the main window should be opened or brought forward.
    @Published private(set) var mainWindowRequest = 0

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

    private(set) lazy var menuBarController = MenuBarController(
        controllers: allControllers,
        purchaseManager: purchaseManager,
        showMainWindow: { [weak self] kind in
            self?.showMainWindow(selecting: kind.map(SidebarItem.timer))
        }
    )

    private var hasStarted = false
    private var cancellables = Set<AnyCancellable>()

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

        let purchaseManager = PurchaseManager(settingsStore: settingsStore)
        self.purchaseManager = purchaseManager

        self.countdownControllers = [
            TimerController(kind: .countdown, settingsStore: settingsStore, fileAccess: fileAccess, canUseProFeatures: { purchaseManager.isPro }),
            TimerController(kind: .countdown2, settingsStore: settingsStore, fileAccess: fileAccess, canUseProFeatures: { purchaseManager.isPro }),
            TimerController(kind: .countdown3, settingsStore: settingsStore, fileAccess: fileAccess, canUseProFeatures: { purchaseManager.isPro }),
            TimerController(kind: .countdown4, settingsStore: settingsStore, fileAccess: fileAccess, canUseProFeatures: { purchaseManager.isPro }),
        ]

        self.countUpControllers = [
            TimerController(kind: .countup, settingsStore: settingsStore, fileAccess: fileAccess, canUseProFeatures: { purchaseManager.isPro }),
            TimerController(kind: .countup2, settingsStore: settingsStore, fileAccess: fileAccess, canUseProFeatures: { purchaseManager.isPro }),
        ]

        self.timeController = TimerController(
            kind: .time,
            settingsStore: settingsStore,
            fileAccess: fileAccess,
            canUseProFeatures: { purchaseManager.isPro }
        )

        purchaseManager.objectWillChange
            .sink { [weak self] _ in
                self?.objectWillChange.send()
            }
            .store(in: &cancellables)

        settingsStore.objectWillChange
            .sink { [weak self] _ in
                self?.objectWillChange.send()
            }
            .store(in: &cancellables)
    }

    /// Runs once per launch. It is called at launch and again whenever the main window
    /// appears, because the app can launch, and keep running, without that window.
    func startup() async {
        WindowManager.applyStayOnTop(settingsStore.stayOnTop)

        guard !hasStarted else { return }
        hasStarted = true

        settingsStore.timesUsed += 1

        if settingsStore.timesUsed == 10 {
            SKStoreReviewController.requestReview()
        }

        if settingsStore.timesUsed > 1 && !settingsStore.hasSeenWelcomeBack {
            settingsStore.hasSeenWelcomeBack = true
            showWelcomeBack = true
        }

        menuBarController.start()

        await purchaseManager.start()
        allControllers
            .filter(\.autoStart)
            .forEach { $0.start() }

        // With window restoration on, the app can relaunch without ever creating its
        // window. That is fine while a timer is in the menu bar; otherwise open it.
        if !menuBarController.hasVisibleItems, WindowManager.contentWindows.isEmpty {
            showMainWindow()
        }
    }

    func showMainWindow(selecting item: SidebarItem? = nil) {
        if let item {
            selectedItem = item
        }
        mainWindowRequest &+= 1
    }

    func setShowInMenuBar(_ isOn: Bool, for controller: TimerController) {
        guard !isOn || purchaseManager.isPro else {
            showAlert(
                title: "Pro Feature",
                message: "Menu bar timers are a Pro feature. Upgrade to Pro to keep any timer in the menu bar with quick controls."
            )
            selectedItem = .pro
            return
        }

        controller.showInMenuBar = isOn
        controller.persist(restartTimer: false)
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
                refreshRunningTimerDestinations()
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
        refreshRunningTimerDestinations()
        showAlert(
            title: "Folder Reset",
            message: "The output folder has been reset to the default app Documents location."
        )
    }

    func validateOutputFolder() {
        Task {
            do {
                try await fileAccess.validateDirectory(path: settingsStore.directoryPath)
                showAlert(
                    title: "Success",
                    message: "This directory is writable and ready to use for timer outputs."
                )
            } catch {
                showAlert(title: "Folder Validation", message: error.localizedDescription)
            }
        }
    }

    private func refreshRunningTimerDestinations() {
        allControllers.forEach { $0.refreshOutputDestination() }
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
                message: "\(controller(for: command.kind).effectiveTitle) requires Pro."
            )
            return
        }

        selectedItem = .timer(command.kind)
        if let controller = controllerLookup[command.kind] {
            Task {
                await controller.apply(command)
            }
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

    @available(macOS 15.0, *)
    func redeemOfferCode() async {
        guard let viewController = NSApp.keyWindow?.contentViewController
            ?? NSApp.mainWindow?.contentViewController
        else {
            showAlert(title: "Offer Code", message: "Open the main app window and try again.")
            return
        }

        do {
            try await AppStore.presentOfferCodeRedeemSheet(from: viewController)
            await purchaseManager.refreshPurchaseStatus()
            showAlert(title: "Offer Code", message: "Your purchase status has been refreshed.")
        } catch {
            showAlert(
                title: "Offer Code",
                message: "The offer code couldn't be redeemed: \(error.localizedDescription)"
            )
        }
    }
}
