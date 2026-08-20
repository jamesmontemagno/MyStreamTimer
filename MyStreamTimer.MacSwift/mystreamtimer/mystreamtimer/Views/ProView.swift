import SwiftUI

// MARK: - Pro

struct ProWorkspaceView: View {
    @EnvironmentObject private var appModel: AppModel

    var body: some View {
        WorkspaceContainer {
            WorkspaceHeader(
                eyebrow: "Upgrade",
                title: "My Stream Timer Pro",
                subtitle: "Unlock additional timers and advanced features."
            )

            SectionCard(title: "Status", subtitle: "Your current plan.") {
                VStack(alignment: .leading, spacing: 8) {
                    Label(
                        appModel.purchaseManager.isPro ? "Pro is active" : "Free",
                        systemImage: appModel.purchaseManager.isPro ? "checkmark.seal.fill" : "lock"
                    )
                    .font(.title3.weight(.semibold))
                    .foregroundStyle(appModel.purchaseManager.isPro ? .green : .secondary)

                    if appModel.purchaseManager.isLoading {
                        ProgressView("Checking the App Store…")
                    }

                    if let expiration = appModel.purchaseManager.subscriptionExpiration {
                        Text("Active until \(expiration.formatted(date: .abbreviated, time: .shortened))")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }

                    if let storeMessage = appModel.purchaseManager.storeMessage {
                        Text(storeMessage)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                }
            }

            SectionCard(title: "What's included", subtitle: "Everything you get with Pro.") {
                VStack(alignment: .leading, spacing: 8) {
                    BulletRow(text: "Countdown 4, Count Up 2, and Current Time output")
                    BulletRow(text: "Auto, total seconds, and total minutes output formats")
                    BulletRow(text: "Pop-out timer preview windows with customizable font, text color, and background")
                    BulletRow(text: "All automation commands for every timer")
                    BulletRow(text: "Support ongoing development")
                }
            }

            LazyVGrid(columns: [GridItem(.adaptive(minimum: 250), spacing: 16)], spacing: 16) {
                PurchaseOptionCard(
                    title: "Lifetime",
                    subtitle: "One-time purchase, yours forever.",
                    productID: PurchaseManager.lifetimeID,
                    accent: .purple
                )

                PurchaseOptionCard(
                    title: "Monthly",
                    subtitle: "Billed every month.",
                    productID: PurchaseManager.oneMonthSubscriptionID,
                    accent: .blue
                )

                PurchaseOptionCard(
                    title: "6 Months",
                    subtitle: "Best value subscription.",
                    productID: PurchaseManager.sixMonthSubscriptionID,
                    accent: .green
                )
            }

            HStack {
                Button {
                    Task { await appModel.restorePurchases() }
                } label: {
                    Label("Restore Purchases", systemImage: "arrow.clockwise")
                }
                .buttonStyle(AppActionButtonStyle(prominent: true))

                if #available(macOS 15.0, *) {
                    Button {
                        Task { await appModel.redeemOfferCode() }
                    } label: {
                        Label("Redeem Offer Code", systemImage: "ticket")
                    }
                    .buttonStyle(AppActionButtonStyle())
                }

                if appModel.purchaseManager.isPro {
                    Button {
                        appModel.openExternalURL("https://support.apple.com/HT202039")
                    } label: {
                        Label("Manage Subscription", systemImage: "gearshape")
                    }
                    .buttonStyle(AppActionButtonStyle())
                }

                Spacer()
            }
        }
    }
}
