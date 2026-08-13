import AppKit
import Foundation

private struct DirectoryAccessSnapshot: Sendable {
    let path: String
    let defaultPath: String
    let bookmarkData: Data?
}

private actor BookmarkFileWorker {
    func validateDirectory(_ snapshot: DirectoryAccessSnapshot) throws -> Data? {
        try withDirectoryAccess(snapshot) { url in
            try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
            let tempFile = url.appendingPathComponent(UUID().uuidString)
            try "test".write(to: tempFile, atomically: true, encoding: .utf8)
            try FileManager.default.removeItem(at: tempFile)
        }
    }

    func initializeFile(
        named fileName: String,
        snapshot: DirectoryAccessSnapshot
    ) throws -> Data? {
        try withDirectoryAccess(snapshot) { url in
            try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
            let outputURL = url.appendingPathComponent(fileName)
            if !FileManager.default.fileExists(atPath: outputURL.path) {
                try "".write(to: outputURL, atomically: true, encoding: .utf8)
            }
        }
    }

    func write(
        text: String,
        fileName: String,
        snapshot: DirectoryAccessSnapshot
    ) throws -> Data? {
        try withDirectoryAccess(snapshot) { url in
            try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
            let outputURL = url.appendingPathComponent(fileName)
            try text.write(to: outputURL, atomically: true, encoding: .utf8)
        }
    }

    private func withDirectoryAccess(
        _ snapshot: DirectoryAccessSnapshot,
        operation: (URL) throws -> Void
    ) throws -> Data? {
        let (resolvedURL, refreshedBookmark) = try resolveURL(for: snapshot)
        let hasAccess = resolvedURL.startAccessingSecurityScopedResource()
        defer {
            if hasAccess {
                resolvedURL.stopAccessingSecurityScopedResource()
            }
        }
        try operation(resolvedURL)
        return refreshedBookmark
    }

    private func resolveURL(for snapshot: DirectoryAccessSnapshot) throws -> (URL, Data?) {
        guard snapshot.path != snapshot.defaultPath, let bookmarkData = snapshot.bookmarkData else {
            return (URL(fileURLWithPath: snapshot.path, isDirectory: true), nil)
        }

        var isStale = false
        let resolvedURL = try URL(
            resolvingBookmarkData: bookmarkData,
            options: [.withSecurityScope],
            relativeTo: nil,
            bookmarkDataIsStale: &isStale
        )

        let refreshedBookmark = isStale
            ? try resolvedURL.bookmarkData(
                options: [.withSecurityScope],
                includingResourceValuesForKeys: nil,
                relativeTo: nil
            )
            : nil
        return (resolvedURL, refreshedBookmark)
    }
}

@MainActor
final class BookmarkFileAccess {
    private let settingsStore: LegacySettingsStore
    private let worker = BookmarkFileWorker()

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

    func validateDirectory(path: String) async throws {
        let refreshedBookmark = try await worker.validateDirectory(snapshot(path: path))
        applyRefreshedBookmark(refreshedBookmark)
    }

    func initializeFile(named fileName: String) async throws {
        let refreshedBookmark = try await worker.initializeFile(
            named: fileName,
            snapshot: snapshot(path: settingsStore.directoryPath)
        )
        applyRefreshedBookmark(refreshedBookmark)
    }

    func write(text: String, fileName: String) async throws {
        let refreshedBookmark = try await worker.write(
            text: text,
            fileName: fileName,
            snapshot: snapshot(path: settingsStore.directoryPath)
        )
        applyRefreshedBookmark(refreshedBookmark)
    }

    private func snapshot(path: String) -> DirectoryAccessSnapshot {
        DirectoryAccessSnapshot(
            path: path,
            defaultPath: settingsStore.defaultDirectoryPath,
            bookmarkData: settingsStore.bookmarkData
        )
    }

    private func applyRefreshedBookmark(_ refreshedBookmark: Data?) {
        if let refreshedBookmark {
            settingsStore.bookmarkData = refreshedBookmark
        }
    }
}
