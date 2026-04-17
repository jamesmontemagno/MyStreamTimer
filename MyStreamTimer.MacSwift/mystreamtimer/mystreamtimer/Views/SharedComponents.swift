import SwiftUI

// MARK: - Shared components

struct WorkspaceContainer<Content: View>: View {
    @ViewBuilder let content: Content

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                content
            }
            .frame(maxWidth: 1180, alignment: .leading)
            .padding(24)
        }
    }
}

struct WorkspaceHeader: View {
    let eyebrow: String
    let title: String
    let subtitle: String

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(eyebrow.uppercased())
                .font(.caption.weight(.bold))
                .foregroundStyle(.secondary)

            Text(title)
                .font(.largeTitle.bold())

            Text(subtitle)
                .foregroundStyle(.secondary)
        }
    }
}

struct SectionCard<Content: View>: View {
    let title: String
    let subtitle: String
    @ViewBuilder let content: Content

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            VStack(alignment: .leading, spacing: 2) {
                Text(title)
                    .font(.title3.weight(.semibold))
                Text(subtitle)
                    .foregroundStyle(.secondary)
            }

            content
        }
        .padding(18)
        .background(
            RoundedRectangle(cornerRadius: 18, style: .continuous)
                .fill(Color(nsColor: .controlBackgroundColor))
        )
        .overlay(
            RoundedRectangle(cornerRadius: 18, style: .continuous)
                .strokeBorder(.quaternary, lineWidth: 1)
        )
    }
}

struct StatusChip: View {
    let title: String
    let tint: Color

    var body: some View {
        Text(title)
            .font(.caption.weight(.semibold))
            .padding(.horizontal, 8)
            .padding(.vertical, 4)
            .background(tint.opacity(0.16), in: Capsule())
            .foregroundStyle(tint)
    }
}

struct BulletRow: View {
    let text: String

    var body: some View {
        Label(text, systemImage: "checkmark.circle.fill")
            .foregroundStyle(.primary)
    }
}

struct PurchaseOptionCard: View {
    @EnvironmentObject private var appModel: AppModel

    let title: String
    let subtitle: String
    let productID: String
    let accent: Color

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(title)
                .font(.title3.weight(.semibold))

            Text(appModel.purchaseManager.priceLabel(for: productID))
                .font(.title2.weight(.bold))
                .foregroundStyle(accent)

            Text(subtitle)
                .foregroundStyle(.secondary)

            Button("Purchase") {
                Task { await appModel.purchase(productID: productID) }
            }
            .buttonStyle(.borderedProminent)
            .tint(accent)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(16)
        .background(
            RoundedRectangle(cornerRadius: 16, style: .continuous)
                .fill(Color(nsColor: .controlBackgroundColor))
        )
        .overlay(
            RoundedRectangle(cornerRadius: 16, style: .continuous)
                .strokeBorder(.quaternary, lineWidth: 1)
        )
    }
}

// MARK: - TimerController view helpers

extension TimerController {
    var statusLabel: String {
        if isPaused { return "Paused" }
        if isRunning { return "Running" }
        return "Ready"
    }

    var statusTint: Color {
        if isPaused { return .orange }
        if isRunning { return .green }
        return .secondary
    }

    var previewDisplayText: String {
        if !currentText.isEmpty { return currentText }
        if kind == .time { return showAMPM ? "9:41 AM" : "9:41" }
        return "Not running"
    }
}
