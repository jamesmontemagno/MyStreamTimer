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
                    Link("GitHub", destination: URL(string: "https://github.com/jamesmontemagno/mystreamtimer")!)
                    Link("Website", destination: URL(string: "https://www.mystreamtimer.com")!)
                    Link("Terms of Use", destination: URL(string: "https://www.apple.com/legal/internet-services/itunes/dev/stdeula/")!)
                }
            }
        }
    }
}
