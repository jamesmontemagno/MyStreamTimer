import SwiftUI

struct WelcomeBackView: View {
    @EnvironmentObject private var appModel: AppModel
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(spacing: 0) {
            // Header
            VStack(spacing: 8) {
                Text("👋")
                    .font(.system(size: 48))

                Text("Welcome Back!")
                    .font(.largeTitle.bold())

                Text("My Stream Timer has been completely redesigned. Here's what's new.")
                    .font(.body)
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
            }
            .padding(.top, 32)
            .padding(.horizontal, 32)

            // Feature rows
            VStack(spacing: 12) {
                FeatureRow(
                    icon: "sparkles",
                    iconColor: .purple,
                    title: "Fresh Redesign",
                    description: "A completely new look built natively for macOS with a clean sidebar layout."
                )

                FeatureRow(
                    icon: "paintpalette.fill",
                    iconColor: .blue,
                    title: "New Themes",
                    description: "Choose Light, Dark, or follow your System appearance in Settings."
                )

                FeatureRow(
                    icon: "macwindow.on.rectangle",
                    iconColor: .orange,
                    title: "Pop-Out Previews",
                    description: "Float a live timer preview anywhere on your screen. Available with Pro."
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
                    Label("Get Started", systemImage: "arrow.right")
                }
                .buttonStyle(AppActionButtonStyle(prominent: true))
                .keyboardShortcut(.defaultAction)
            }
            .padding(20)
        }
        .frame(width: 460)
    }
}

private struct FeatureRow: View {
    let icon: String
    let iconColor: Color
    let title: String
    let description: String

    var body: some View {
        HStack(alignment: .top, spacing: 16) {
            Image(systemName: icon)
                .font(.title2)
                .foregroundStyle(iconColor)
                .frame(width: 36, height: 36)
                .background(iconColor.opacity(0.12), in: RoundedRectangle(cornerRadius: 10, style: .continuous))

            VStack(alignment: .leading, spacing: 3) {
                Text(title)
                    .font(.headline)
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
