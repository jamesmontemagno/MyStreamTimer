import SwiftUI

// MARK: - About

struct AboutWorkspaceView: View {
    private var versionText: String {
        let version = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "1.0"
        let build = Bundle.main.object(forInfoDictionaryKey: "CFBundleVersion") as? String ?? "1"
        return "Version \(version) (\(build))"
    }

    var body: some View {
        WorkspaceContainer {
            WorkspaceHeader(
                eyebrow: "About",
                title: "My Stream Timer",
                subtitle: "Dependable timer overlays for streamers on macOS."
            )

            SectionCard(title: versionText, subtitle: "Built for OBS, Stream Deck, and desktop automation.") {
                HStack {
                    Link(destination: URL(string: "https://github.com/jamesmontemagno/mystreamtimer")!) {
                        Label("GitHub", systemImage: "chevron.left.forwardslash.chevron.right")
                    }
                    .buttonStyle(AppActionButtonStyle())

                    Link(destination: URL(string: "https://www.mystreamtimer.com")!) {
                        Label("Website", systemImage: "globe")
                    }
                    .buttonStyle(AppActionButtonStyle())

                    Link(destination: URL(string: "https://www.apple.com/legal/internet-services/itunes/dev/stdeula/")!) {
                        Label("Terms of Use", systemImage: "doc.text")
                    }
                    .buttonStyle(AppActionButtonStyle())
                }
            }
        }
    }
}
