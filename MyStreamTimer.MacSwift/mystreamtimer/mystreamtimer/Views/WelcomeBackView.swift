import SwiftUI

struct WelcomeBackView: View {
    @EnvironmentObject private var appModel: AppModel
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(spacing: 0) {
            // Header
            VStack(spacing: 8) {
                Text("🎉")
                    .font(.system(size: 48))

                Text("What's New in 3.1.0")
                    .font(.largeTitle.bold())

                Text("Here's what was added to My Stream Timer in this update.")
                    .font(.body)
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
            }
            .padding(.top, 32)
            .padding(.horizontal, 32)

            // Feature rows
            VStack(spacing: 12) {
                FeatureRow(
                    icon: "menubar.rectangle",
                    iconColor: .purple,
                    title: "Menu Bar Timers",
                    description: "Keep any timer in the menu bar with quick controls, even when the window is closed.",
                    isPro: true
                )

                FeatureRow(
                    icon: "speaker.wave.2.fill",
                    iconColor: .orange,
                    title: "Custom End Sounds",
                    description: "Give each timer its own sound: Chime, Bell, Digital, or your own MP3 or WAV."
                )
            }
            .padding(24)

            Divider()

            // Actions
            HStack {
                if !appModel.purchaseManager.isPro {
                    Button {
                        dismiss()
                        appModel.selectedItem = .pro
                    } label: {
                        Label("Learn About Pro", systemImage: "sparkles")
                    }
                    .buttonStyle(AppActionButtonStyle())
                }

                Spacer()

                Button {
                    dismiss()
                } label: {
                    Label("Continue", systemImage: "arrow.right")
                }
                .buttonStyle(AppActionButtonStyle(prominent: true))
                .keyboardShortcut(.defaultAction)
            }
            .padding(20)
        }
        .frame(width: 460)
        .onAppear {
            appModel.settingsStore.hasSeenWelcomeBack = true
        }
    }
}

private struct FeatureRow: View {
    let icon: String
    let iconColor: Color
    let title: String
    let description: String
    var isPro = false

    var body: some View {
        HStack(alignment: .top, spacing: 16) {
            Image(systemName: icon)
                .font(.title2)
                .foregroundStyle(iconColor)
                .frame(width: 36, height: 36)
                .background(iconColor.opacity(0.12), in: RoundedRectangle(cornerRadius: 10, style: .continuous))

            VStack(alignment: .leading, spacing: 3) {
                HStack(spacing: 6) {
                    Text(title)
                        .font(.headline)
                    if isPro {
                        StatusChip(title: "PRO", tint: .yellow)
                    }
                }
                Text(description)
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }

            Spacer(minLength: 0)
        }
        .padding(14)
        .background(
            RoundedRectangle(cornerRadius: 14, style: .continuous)
                .fill(Color(nsColor: .controlBackgroundColor))
        )
        .overlay(
            RoundedRectangle(cornerRadius: 14, style: .continuous)
                .strokeBorder(.quaternary, lineWidth: 1)
        )
    }
}
