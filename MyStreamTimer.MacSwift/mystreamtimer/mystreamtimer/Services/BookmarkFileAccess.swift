import AppKit
import Foundation

@MainActor
final class BookmarkFileAccess {
    private let settingsStore: LegacySettingsStore

    init(settingsStore: LegacySettingsStore) {
        self.settingsStore = settingsStore
    }

    func chooseDirectory() throws -> String? {
        let panel = NSOpenPanel()
        panel.canChooseDirectories = true
        panel.canChooseFiles = false
        panel.canCreateDirectories = true
        panel.allowsMultipleSelection = false
        panel.title = "Select a folder for timer output files"

        guard panel.runModal() == .OK, let url = panel.url else {
            return nil
        }

        if url.path == settingsStore.defaultDirectoryPath {
            settingsStore.bookmarkData = nil
        } else {
            let bookmark = try url.bookmarkData(
                options: [.withSecurityScope],
                includingResourceValuesForKeys: nil,
                relativeTo: nil
            )
            settingsStore.bookmarkData = bookmark
        }

        return url.path
    }

    func resetToDefaultDirectory() {
        settingsStore.bookmarkData = nil
        settingsStore.updateDirectoryPath(settingsStore.defaultDirectoryPath)
    }

    func validateDirectory(path: String) throws {
        try withDirectoryAccess(path: path) { url in
            try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
            let tempFile = url.appendingPathComponent(UUID().uuidString)
            try "test".write(to: tempFile, atomically: true, encoding: .utf8)
            try FileManager.default.removeItem(at: tempFile)
        }
    }

    func initializeFile(named fileName: String) throws {
        try withDirectoryAccess(path: settingsStore.directoryPath) { url in
            try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
            let outputURL = url.appendingPathComponent(fileName)
            if !FileManager.default.fileExists(atPath: outputURL.path) {
                try "".write(to: outputURL, atomically: true, encoding: .utf8)
            }
        }
    }

    func write(text: String, fileName: String) throws {
        try withDirectoryAccess(path: settingsStore.directoryPath) { url in
            try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
            let outputURL = url.appendingPathComponent(fileName)
            try text.write(to: outputURL, atomically: true, encoding: .utf8)
        }
    }

    private func withDirectoryAccess<T>(path: String, _ operation: (URL) throws -> T) throws -> T {
        let resolvedURL = try resolveURL(for: path)
        let hasAccess = resolvedURL.startAccessingSecurityScopedResource()
        defer {
            if hasAccess {
                resolvedURL.stopAccessingSecurityScopedResource()
            }
        }
        return try operation(resolvedURL)
    }

    private func resolveURL(for path: String) throws -> URL {
        if path == settingsStore.defaultDirectoryPath || settingsStore.bookmarkData == nil {
            return URL(fileURLWithPath: path, isDirectory: true)
        }

        var isStale = false
        let resolvedURL = try URL(
            resolvingBookmarkData: settingsStore.bookmarkData ?? Data(),
            options: [.withSecurityScope],
            relativeTo: nil,
            bookmarkDataIsStale: &isStale
        )

        if isStale {
            settingsStore.bookmarkData = try resolvedURL.bookmarkData(
                options: [.withSecurityScope],
                includingResourceValuesForKeys: nil,
                relativeTo: nil
            )
        }

        return resolvedURL
    }
}
