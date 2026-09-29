import Foundation

enum EndSound: String, CaseIterable, Identifiable {
    case defaultBeep = "default"
    case chime
    case bell
    case digital
    case custom

    var id: String { rawValue }

    var displayName: String {
        switch self {
        case .defaultBeep: "Default beep"
        case .chime: "Chime"
        case .bell: "Bell"
        case .digital: "Digital"
        case .custom: "Custom"
        }
    }

    static let supportedExtensions = ["mp3", "wav", "wave"]

    static func supports(_ url: URL) -> Bool {
        url.isFileURL && supportedExtensions.contains(url.pathExtension.lowercased())
    }

    func bundledURL(in bundle: Bundle = .main) -> URL? {
        guard self != .custom else { return nil }
        let name = "end-\(rawValue)"
        return bundle.url(forResource: name, withExtension: "wav")
            ?? bundle.url(forResource: name, withExtension: "wav", subdirectory: "Sounds")
            ?? bundle.url(forResource: name, withExtension: "wav", subdirectory: "Resources/Sounds")
    }

    func resolve<Value>(
        using load: (EndSound) throws -> Value
    ) throws -> (value: Value, usedFallback: Bool) {
        do {
            return (try load(self), false)
        } catch {
            guard self != .defaultBeep else { throw error }
            return (try load(.defaultBeep), true)
        }
    }
}
