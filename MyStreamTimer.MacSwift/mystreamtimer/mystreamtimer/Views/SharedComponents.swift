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
                .fill(Color(nsColor: .textBackgroundColor))
        )
        .overlay(
            RoundedRectangle(cornerRadius: 18, style: .continuous)
                .strokeBorder(Color(nsColor: .separatorColor).opacity(0.7), lineWidth: 1)
        )
        .shadow(color: .black.opacity(0.04), radius: 8, y: 2)
    }
}

struct AppActionButtonStyle: ButtonStyle {
    let prominent: Bool
    let tint: Color

    @Environment(\.isEnabled) private var isEnabled
    @Environment(\.colorScheme) private var colorScheme

    init(prominent: Bool = false, tint: Color = .accentColor) {
        self.prominent = prominent
        self.tint = tint
    }

    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.body.weight(.semibold))
            .padding(.horizontal, 14)
            .frame(minHeight: 34)
            .foregroundStyle(prominent ? Color.white : Color.primary)
            .background(
                Capsule()
                    .fill(
                        prominent
                            ? tint
                            : Color.primary.opacity(colorScheme == .dark ? 0.14 : 0.07)
                    )
            )
            .overlay {
                Capsule()
                    .strokeBorder(
                        prominent
                            ? tint.opacity(0.9)
                            : Color(nsColor: .separatorColor).opacity(0.8),
                        lineWidth: 1
                    )
            }
            .opacity(isEnabled ? 1 : 0.45)
            .scaleEffect(configuration.isPressed ? 0.97 : 1)
            .animation(.easeOut(duration: 0.12), value: configuration.isPressed)
    }
}

struct LeadingToggleRow<Label: View>: View {
    @Binding var isOn: Bool
    @ViewBuilder let label: Label

    var body: some View {
        HStack(alignment: .top, spacing: 12) {
            Toggle("", isOn: $isOn)
                .labelsHidden()
                .toggleStyle(.switch)

            label

            Spacer(minLength: 0)
        }
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

    private var isOwned: Bool {
        appModel.purchaseManager.entitledProductIDs.contains(productID)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Text(title)
                    .font(.title3.weight(.semibold))

                if isOwned {
                    StatusChip(title: "Active", tint: .green)
                }
            }

            Text(appModel.purchaseManager.priceLabel(for: productID))
                .font(.title2.weight(.bold))
                .foregroundStyle(isOwned ? .green : accent)

            Text(isOwned ? "You own this plan." : subtitle)
                .foregroundStyle(.secondary)

            if !isOwned {
                Button {
                    Task { await appModel.purchase(productID: productID) }
                } label: {
                    Label("Purchase", systemImage: "cart.fill")
                }
                .buttonStyle(AppActionButtonStyle(prominent: true, tint: accent))
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(16)
        .background(
            RoundedRectangle(cornerRadius: 16, style: .continuous)
                .fill(isOwned ? accent.opacity(0.06) : Color(nsColor: .controlBackgroundColor))
        )
        .overlay(
            RoundedRectangle(cornerRadius: 16, style: .continuous)
                .strokeBorder(isOwned ? AnyShapeStyle(accent.opacity(0.4)) : AnyShapeStyle(.quaternary), lineWidth: isOwned ? 2 : 1)
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
