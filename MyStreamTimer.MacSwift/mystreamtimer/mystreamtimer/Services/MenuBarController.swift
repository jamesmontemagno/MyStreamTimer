import AppKit
import Combine

/// Owns one menu bar item for every timer that is switched on for the menu bar, and keeps
/// each item in step with its timer. Items exist only while the user has Pro.
///
/// This uses AppKit status items rather than SwiftUI's `MenuBarExtra` because a
/// `MenuBarExtra` label ignores `monospacedDigit()`, so the item would change width as
/// the digits change and push its neighbours around every second.
final class MenuBarController {
    private let controllers: [TimerController]
    private let purchaseManager: PurchaseManager
    private let showMainWindow: (TimerKind?) -> Void

    private var items: [TimerKind: MenuBarTimerItem] = [:]
    private var cancellables = Set<AnyCancellable>()
    private var isRefreshScheduled = false

    var hasVisibleItems: Bool {
        !items.isEmpty
    }

    static func shouldShowItem(isEnabled: Bool, isPro: Bool) -> Bool {
        isEnabled && isPro
    }

    init(
        controllers: [TimerController],
        purchaseManager: PurchaseManager,
        showMainWindow: @escaping (TimerKind?) -> Void
    ) {
        self.controllers = controllers
        self.purchaseManager = purchaseManager
        self.showMainWindow = showMainWindow
    }

    func start() {
        guard cancellables.isEmpty else { return }

        for controller in controllers {
            controller.objectWillChange
                .sink { [weak self] _ in self?.scheduleRefresh() }
                .store(in: &cancellables)
        }
        purchaseManager.objectWillChange
            .sink { [weak self] _ in self?.scheduleRefresh() }
            .store(in: &cancellables)

        refresh()
    }

    /// `objectWillChange` fires before the new value is stored, so read it on the next pass.
    private func scheduleRefresh() {
        guard !isRefreshScheduled else { return }
        isRefreshScheduled = true

        DispatchQueue.main.async { [weak self] in
            guard let self else { return }
            self.isRefreshScheduled = false
            self.refresh()
        }
    }

    private func refresh() {
        let isPro = purchaseManager.isPro
        let hadVisibleItems = hasVisibleItems

        for controller in controllers {
            let kind = controller.kind
            if Self.shouldShowItem(isEnabled: controller.showInMenuBar, isPro: isPro) {
                if let item = items[kind] {
                    item.update()
                } else {
                    items[kind] = MenuBarTimerItem(
                        controller: controller,
                        showMainWindow: { [weak self] in self?.showMainWindow(kind) }
                    )
                }
            } else if let item = items.removeValue(forKey: kind) {
                item.remove()
            }
        }

        // The app stays open without a window only while it has a menu bar item, so don't
        // leave it running with neither. A hidden app gets its windows back when it is shown.
        if hadVisibleItems, !hasVisibleItems, !NSApp.isHidden, !WindowManager.hasOpenContentWindow {
            showMainWindow(nil)
        }
    }
}

// MARK: - Menu actions

/// What a timer's menu items do. A menu keeps the titles it opened with while its timer
/// carries on, so each action checks the timer's state again and does nothing when it no
/// longer applies. A "Stop" left on screen after a countdown finishes must not start it.
struct MenuBarTimerActions {
    let controller: TimerController

    /// Returns false when the timer was stopped and could not be started.
    @discardableResult
    func start() -> Bool {
        guard !controller.isRunning else { return true }
        controller.start()
        return controller.isRunning
    }

    /// Returns the task doing the stop, or nil when the timer was not running.
    @discardableResult
    func stop() -> Task<Void, Never>? {
        guard controller.isRunning else { return nil }
        let controller = controller
        return Task {
            await controller.stop(clearOutput: true)
        }
    }

    func pause() {
        guard controller.canPauseResume, !controller.isPaused else { return }
        controller.pauseResume()
    }

    func resume() {
        guard controller.canPauseResume, controller.isPaused else { return }
        controller.pauseResume()
    }
}

// MARK: - One timer's menu bar item

private final class MenuBarTimerItem: NSObject, NSMenuDelegate {
    private let controller: TimerController
    private let actions: MenuBarTimerActions
    private let showMainWindow: () -> Void
    private let statusItem: NSStatusItem
    private var visibilityObservation: NSKeyValueObservation?
    private var tickTimer: Timer?
    private var currentSymbolName: String?

    init(controller: TimerController, showMainWindow: @escaping () -> Void) {
        self.controller = controller
        self.actions = MenuBarTimerActions(controller: controller)
        self.showMainWindow = showMainWindow
        self.statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        super.init()

        // The autosave name restores the item's position, and also a hidden state left
        // behind if the user dragged it out of the menu bar, so show it again explicitly.
        statusItem.autosaveName = "MenuBarTimer_\(controller.kind.rawValue)"
        statusItem.behavior = .removalAllowed
        statusItem.isVisible = true

        if let button = statusItem.button {
            button.imagePosition = .imageLeading
            button.font = .monospacedDigitSystemFont(
                ofSize: NSFont.menuBarFont(ofSize: 0).pointSize,
                weight: .regular
            )
        }

        let menu = NSMenu()
        menu.autoenablesItems = false
        menu.delegate = self
        statusItem.menu = menu

        // Dragging the item out of the menu bar hides it; treat that as switching it off.
        visibilityObservation = statusItem.observe(\.isVisible, options: [.new]) { [weak self] _, change in
            guard change.newValue == false else { return }
            DispatchQueue.main.async {
                self?.hideFromMenuBar()
            }
        }

        update()
    }

    func remove() {
        visibilityObservation?.invalidate()
        visibilityObservation = nil
        tickTimer?.invalidate()
        tickTimer = nil
        NSStatusBar.system.removeStatusItem(statusItem)
    }

    func update() {
        let now = Date()
        let timeText = timeText(at: now)
        let symbolName = controller.isPaused ? "pause.fill" : controller.effectiveSystemImage

        if let button = statusItem.button {
            if symbolName != currentSymbolName {
                currentSymbolName = symbolName
                button.image = Self.symbolImage(named: symbolName, fallback: controller.kind.systemImage)
            }
            if button.title != timeText {
                button.title = timeText
            }

            let summary = "\(controller.effectiveTitle): \(controller.statusLabel)"
            if button.toolTip != summary {
                button.toolTip = summary
            }
            button.setAccessibilityLabel(timeText.isEmpty ? summary : "\(summary), \(timeText)")
        }

        scheduleTick(after: now)
    }

    /// Empty while the timer is stopped, so an idle item is just its icon.
    private func timeText(at date: Date) -> String {
        guard controller.isRunning else { return "" }

        if controller.kind == .time {
            let style = controller.effectiveOutputStyle
            return MenuBarTimeFormatter.clockString(
                for: date,
                uses24Hour: style == 2 || style == 3,
                showAMPM: controller.showAMPM
            )
        }
        return MenuBarTimeFormatter.string(for: controller.displayInterval(at: date) ?? 0)
    }

    /// Wakes on this timer's own next second (or the clock's next minute) instead of a
    /// shared tick, so the item changes at the same moment as the timer's output file.
    private func scheduleTick(after date: Date) {
        tickTimer?.invalidate()
        tickTimer = nil

        guard controller.isRunning, !controller.isPaused else { return }

        let delay: TimeInterval
        if controller.kind == .time {
            delay = MenuBarTimeFormatter.secondsUntilNextMinute(after: date)
        } else if let interval = controller.displayInterval(at: date) {
            delay = MenuBarTimeFormatter.secondsUntilNextChange(
                of: interval,
                countingDown: controller.kind.isCountdown
            )
        } else {
            return
        }

        let timer = Timer(timeInterval: delay + 0.02, repeats: false) { [weak self] _ in
            MainActor.assumeIsolated {
                self?.update()
            }
        }
        // Common modes keep the time moving while a menu is open.
        RunLoop.main.add(timer, forMode: .common)
        tickTimer = timer
    }

    private static func symbolImage(named name: String, fallback: String) -> NSImage? {
        NSImage(systemSymbolName: name, accessibilityDescription: nil)
            ?? NSImage(systemSymbolName: fallback, accessibilityDescription: nil)
    }

    // MARK: Menu

    func menuNeedsUpdate(_ menu: NSMenu) {
        menu.removeAllItems()

        let header = NSMenuItem(
            title: "\(controller.effectiveTitle) · \(controller.statusLabel)",
            action: nil,
            keyEquivalent: ""
        )
        header.isEnabled = false
        menu.addItem(header)
        menu.addItem(.separator())

        // The action is chosen with the title, so an item always does what it says.
        menu.addItem(menuItem(
            controller.startStopTitle,
            action: controller.isRunning ? #selector(stopTimer) : #selector(startTimer)
        ))
        if controller.kind != .time {
            menu.addItem(menuItem(
                controller.pauseResumeTitle,
                action: controller.isPaused ? #selector(resumeTimer) : #selector(pauseTimer),
                isEnabled: controller.canPauseResume
            ))
            menu.addItem(menuItem(
                "Add 1 Minute",
                action: #selector(addMinute),
                isEnabled: controller.isRunning
            ))
            menu.addItem(menuItem(
                "Reset",
                action: #selector(resetTimer),
                isEnabled: controller.isRunning
            ))
        }

        menu.addItem(.separator())
        menu.addItem(menuItem("Open My Stream Timer", action: #selector(openMainWindow)))
        menu.addItem(menuItem("Hide from Menu Bar", action: #selector(hideFromMenuBar)))
        menu.addItem(.separator())
        menu.addItem(menuItem("Quit My Stream Timer", action: #selector(quit)))
    }

    private func menuItem(_ title: String, action: Selector, isEnabled: Bool = true) -> NSMenuItem {
        let item = NSMenuItem(title: title, action: action, keyEquivalent: "")
        item.target = self
        item.isEnabled = isEnabled
        return item
    }

    @objc private func startTimer() {
        if !actions.start() {
            // The reason it didn't start is only shown on the timer's page.
            showMainWindow()
        }
    }

    @objc private func stopTimer() {
        actions.stop()
    }

    @objc private func pauseTimer() {
        actions.pause()
    }

    @objc private func resumeTimer() {
        actions.resume()
    }

    @objc private func addMinute() {
        controller.addMinute()
    }

    @objc private func resetTimer() {
        Task {
            await controller.reset()
        }
    }

    @objc private func openMainWindow() {
        showMainWindow()
    }

    @objc private func hideFromMenuBar() {
        guard controller.showInMenuBar else { return }
        controller.showInMenuBar = false
        controller.persist(restartTimer: false)
    }

    @objc private func quit() {
        NSApp.terminate(nil)
    }
}
