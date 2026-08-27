import AppKit
import Combine
import Foundation
import SwiftUI

@MainActor
final class LegacySettingsStore: ObservableObject {
    private let defaults: UserDefaults

    @Published var directoryPath: String
    @Published var stayOnTop: Bool {
        didSet {
            defaults.set(stayOnTop, forKey: "StayOnTop")
            WindowManager.applyStayOnTop(stayOnTop)
        }
    }

    @Published var popOutFontSize: Double {
        didSet {
            defaults.set(popOutFontSize, forKey: "PopOutFontSize")
        }
    }

    /// Empty string means the system rounded font.
    @Published var popOutFontFamily: String {
        didSet {
            defaults.set(popOutFontFamily, forKey: "PopOutFontFamily")
        }
    }

    @Published var popOutTextColorHex: String {
        didSet {
            defaults.set(popOutTextColorHex, forKey: "PopOutTextColorHex")
        }
    }

    @Published var popOutBackgroundColorHex: String {
        didSet {
            defaults.set(popOutBackgroundColorHex, forKey: "PopOutBackgroundColorHex")
        }
    }

    @Published var theme: AppTheme {
        didSet {
            defaults.set(theme.rawValue, forKey: "AppTheme")
        }
    }

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        let defaultDirectoryPath = Self.defaultDirectoryURL().path
        self.directoryPath = defaults.string(forKey: "global_directory_path") ?? defaultDirectoryPath
        self.stayOnTop = defaults.object(forKey: "StayOnTop") as? Bool ?? false
        self.popOutFontSize = defaults.object(forKey: "PopOutFontSize") as? Double ?? 48
        self.popOutFontFamily = defaults.string(forKey: "PopOutFontFamily") ?? ""
        self.popOutTextColorHex = defaults.string(forKey: "PopOutTextColorHex") ?? "#FFFFFF"
        self.popOutBackgroundColorHex = defaults.string(forKey: "PopOutBackgroundColorHex") ?? "#000000"
        self.theme = AppTheme(rawValue: defaults.string(forKey: "AppTheme") ?? "") ?? .system
    }

    static let availableFontFamilies: [String] = NSFontManager.shared.availableFontFamilies.sorted {
        $0.localizedCaseInsensitiveCompare($1) == .orderedAscending
    }

    var popOutFont: Font {
        let size = popOutFontSize
        guard !popOutFontFamily.isEmpty,
              let nsFont = NSFont(name: popOutFontFamily, size: size) else {
            return .system(size: size, weight: .bold, design: .rounded)
        }
        let bold = NSFontManager.shared.convert(nsFont, toHaveTrait: .boldFontMask)
        return Font(bold)
    }

    static func defaultDirectoryURL() -> URL {
        let documents = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask).first!
        return documents.appendingPathComponent("MyStreamTimer", isDirectory: true)
    }

    var defaultDirectoryPath: String {
        Self.defaultDirectoryURL().path
    }

    var timesUsed: Int {
        get {
            defaults.object(forKey: "TimesUsed") as? Int ?? 0
        }
        set {
            defaults.set(newValue, forKey: "TimesUsed")
        }
    }

    var hasSeenWelcomeBack: Bool {
        get {
            defaults.object(forKey: "HasSeenWelcomeBackV1") as? Bool ?? false
        }
        set {
            defaults.set(newValue, forKey: "HasSeenWelcomeBackV1")
        }
    }

    var bookmarkData: Data? {
        get { defaults.data(forKey: "bookmark") }
        set { defaults.set(newValue, forKey: "bookmark") }
    }

    var hasLegacyProEntitlement: Bool {
        let hasGold = defaults.object(forKey: "IsGold") as? Bool ?? false
        let hasBronze = defaults.object(forKey: "IsBronze") as? Bool ?? false
        let hasSilver = defaults.object(forKey: "IsSilver") as? Bool ?? false
        let hasSubscription = defaults.object(forKey: "HasTippedSub") as? Bool ?? false
        let expiry = legacyDate(forKey: "SubExpirationDate") ?? .distantPast
        return hasGold || hasBronze || hasSilver || (hasSubscription && expiry > Date())
    }

    func updateDirectoryPath(_ newPath: String) {
        directoryPath = newPath
        defaults.set(newPath, forKey: "global_directory_path")
    }

    func syncPurchaseState(
        entitledProductIDs: Set<String>,
        subscriptionExpiration: Date?,
        authoritative: Bool
    ) {
        let hasSubscription = !entitledProductIDs.intersection(PurchaseManager.subscriptionIDs).isEmpty

        let lifetimeKeys = [
            PurchaseManager.bronzeLifetimeID: "IsBronze",
            PurchaseManager.silverLifetimeID: "IsSilver",
            PurchaseManager.lifetimeID: "IsGold",
        ]
        for (productID, key) in lifetimeKeys {
            if entitledProductIDs.contains(productID) {
                defaults.set(true, forKey: key)
            } else if authoritative {
                defaults.set(false, forKey: key)
            }
        }

        if hasSubscription, let subscriptionExpiration {
            defaults.set(true, forKey: "HasTippedSub")
            defaults.set(true, forKey: "CheckSubStatus")
            defaults.set(subscriptionExpiration, forKey: "SubExpirationDate")
        } else if authoritative {
            defaults.set(false, forKey: "HasTippedSub")
            defaults.set(false, forKey: "CheckSubStatus")
            defaults.removeObject(forKey: "SubExpirationDate")
        }
    }

    private func legacyDate(forKey key: String) -> Date? {
        guard let storedValue = defaults.object(forKey: key) else { return nil }
        if let date = storedValue as? Date {
            return date
        }

        let rawTicks: Int64?
        if let string = storedValue as? String {
            rawTicks = Int64(string)
        } else if let number = storedValue as? NSNumber {
            rawTicks = number.int64Value
        } else {
            rawTicks = nil
        }

        guard let rawTicks, rawTicks != .min else { return nil }
        let ticks = rawTicks < 0 ? -rawTicks : rawTicks
        let dotNetTicksAtUnixEpoch: Int64 = 621_355_968_000_000_000
        let secondsSinceUnixEpoch = Double(ticks - dotNetTicksAtUnixEpoch) / 10_000_000
        return Date(timeIntervalSince1970: secondsSinceUnixEpoch)
    }

    func loadConfiguration(for kind: TimerKind) -> TimerConfiguration {
        let keyPrefix = kind.rawValue
        let defaultFinishAt = Calendar.current.date(byAdding: .minute, value: 15, to: Date()) ?? Date()

        return TimerConfiguration(
            minutes: int(forKey: "key_minutes_\(keyPrefix)", default: kind.defaultMinutes),
            seconds: int(forKey: "key_seconds_\(keyPrefix)", default: kind.defaultSeconds),
            useMinutes: bool(forKey: "UseMinutes_\(keyPrefix)", default: true),
            finishAt: dateFromTicks(forKey: "FinishAtTime_\(keyPrefix)") ?? defaultFinishAt,
            output: string(forKey: "key_output_\(keyPrefix)", default: kind.defaultOutput)
                .replacingOccurrences(of: #"\:"#, with: ":"),
            finishText: string(forKey: "key_finish_\(keyPrefix)", default: kind.defaultFinishText),
            fileName: string(forKey: "key_file_name_\(keyPrefix)", default: kind.defaultFileName),
            autoStart: bool(forKey: "key_auto_start_\(keyPrefix)", default: false),
            beepAtZero: bool(forKey: "make_sound_\(keyPrefix)", default: false),
            showAMPM: bool(forKey: "key_show_ampm_\(keyPrefix)", default: false),
            outputStyle: int(forKey: "key_output_style_\(keyPrefix)", default: 0),
            displayName: string(forKey: "DisplayName_\(keyPrefix)", default: ""),
            iconGlyph: string(forKey: "IconGlyph_\(keyPrefix)", default: "")
        )
    }

    func saveConfiguration(_ config: TimerConfiguration, for kind: TimerKind) {
        let keyPrefix = kind.rawValue
        defaults.set(config.minutes, forKey: "key_minutes_\(keyPrefix)")
        defaults.set(config.seconds, forKey: "key_seconds_\(keyPrefix)")
        defaults.set(config.useMinutes, forKey: "UseMinutes_\(keyPrefix)")
        defaults.set(config.output, forKey: "key_output_\(keyPrefix)")
        defaults.set(config.finishText, forKey: "key_finish_\(keyPrefix)")
        defaults.set(config.fileName, forKey: "key_file_name_\(keyPrefix)")
        defaults.set(config.autoStart, forKey: "key_auto_start_\(keyPrefix)")
        defaults.set(config.beepAtZero, forKey: "make_sound_\(keyPrefix)")
        defaults.set(config.showAMPM, forKey: "key_show_ampm_\(keyPrefix)")
        defaults.set(config.outputStyle, forKey: "key_output_style_\(keyPrefix)")
        defaults.set(config.displayName, forKey: "DisplayName_\(keyPrefix)")
        defaults.set(config.iconGlyph, forKey: "IconGlyph_\(keyPrefix)")

        let midnight = Calendar.current.startOfDay(for: config.finishAt)
        let ticks = Int64(config.finishAt.timeIntervalSince(midnight) * 10_000_000)
        defaults.set(ticks, forKey: "FinishAtTime_\(keyPrefix)")
    }

    private func int(forKey key: String, default defaultValue: Int) -> Int {
        guard defaults.object(forKey: key) != nil else { return defaultValue }
        return defaults.integer(forKey: key)
    }

    private func bool(forKey key: String, default defaultValue: Bool) -> Bool {
        guard defaults.object(forKey: key) != nil else { return defaultValue }
        return defaults.bool(forKey: key)
    }

    private func string(forKey key: String, default defaultValue: String) -> String {
        defaults.string(forKey: key) ?? defaultValue
    }

    private func dateFromTicks(forKey key: String) -> Date? {
        guard let value = defaults.object(forKey: key) else { return nil }

        let ticks: Int64?
        if let asInt64 = value as? Int64 {
            ticks = asInt64
        } else if let asInt = value as? Int {
            ticks = Int64(asInt)
        } else if let asNumber = value as? NSNumber {
            ticks = asNumber.int64Value
        } else {
            ticks = nil
        }

        guard let ticks, ticks >= 0 else { return nil }

        let seconds = Double(ticks) / 10_000_000
        let midnight = Calendar.current.startOfDay(for: Date())
        return midnight.addingTimeInterval(seconds)
    }
}
