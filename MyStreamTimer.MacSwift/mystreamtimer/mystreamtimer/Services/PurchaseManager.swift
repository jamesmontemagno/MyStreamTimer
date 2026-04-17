import Combine
import Foundation
import StoreKit

@MainActor
final class PurchaseManager: ObservableObject {
    static let lifetimeID = "mstgold"
    static let oneMonthSubscriptionID = "mstsub"
    static let sixMonthSubscriptionID = "mstsub6months"
    static let subscriptionIDs: Set<String> = [oneMonthSubscriptionID, sixMonthSubscriptionID]

    @Published private(set) var productsByID: [String: Product] = [:]
    @Published private(set) var entitledProductIDs: Set<String> = []
    @Published private(set) var isLoading = false
    @Published private(set) var storeMessage: String?
    @Published private(set) var subscriptionExpiration: Date?

    private let settingsStore: LegacySettingsStore
    private var updatesTask: Task<Void, Never>?

    init(settingsStore: LegacySettingsStore) {
        self.settingsStore = settingsStore
    }

    deinit {
        updatesTask?.cancel()
    }

    var isPro: Bool {
        #if DEBUG
        return true
        #else
        return entitledProductIDs.contains(Self.lifetimeID)
            || !entitledProductIDs.intersection(Self.subscriptionIDs).isEmpty
            || settingsStore.hasLegacyProEntitlement
        #endif
    }

    func start() async {
        updatesTask?.cancel()
        updatesTask = observeTransactions()

        await loadProducts()
        await refreshEntitlements()
    }

    func priceLabel(for productID: String) -> String {
        productsByID[productID]?.displayPrice ?? "Available in App Store"
    }

    func purchase(productID: String) async -> String {
        guard let product = productsByID[productID] else {
            await loadProducts()
            guard let refreshedProduct = productsByID[productID] else {
                let message = "We couldn't load that product right now. Please try again."
                storeMessage = message
                return message
            }
            return await performPurchase(of: refreshedProduct)
        }

        return await performPurchase(of: product)
    }

    func restorePurchases() async -> String {
        do {
            try await AppStore.sync()
            await refreshEntitlements()
            let message = entitledProductIDs.isEmpty
                ? "No active purchases were found to restore."
                : "Your purchase status has been refreshed successfully."
            storeMessage = message
            return message
        } catch {
            let message = "Unable to refresh purchase status: \(error.localizedDescription)"
            storeMessage = message
            return message
        }
    }

    private func loadProducts() async {
        isLoading = true
        defer { isLoading = false }

        do {
            let products = try await Product.products(
                for: [Self.lifetimeID, Self.oneMonthSubscriptionID, Self.sixMonthSubscriptionID]
            )

            var lookup: [String: Product] = [:]
            products.forEach { lookup[$0.id] = $0 }
            productsByID = lookup
            if !lookup.isEmpty {
                storeMessage = nil
            }
        } catch {
            storeMessage = "Unable to load App Store products right now: \(error.localizedDescription)"
        }
    }

    private func performPurchase(of product: Product) async -> String {
        do {
            let result = try await product.purchase()

            switch result {
            case .success(let verificationResult):
                guard case .verified(let transaction) = verificationResult else {
                    let message = "The purchase couldn't be verified."
                    storeMessage = message
                    return message
                }

                await refreshEntitlements()
                await transaction.finish()
                let message = "Thanks! Your Pro status has been updated."
                storeMessage = message
                return message

            case .pending:
                let message = "Your purchase is pending approval."
                storeMessage = message
                return message

            case .userCancelled:
                let message = "Purchase cancelled."
                storeMessage = message
                return message

            @unknown default:
                let message = "Something unexpected happened during the purchase."
                storeMessage = message
                return message
            }
        } catch {
            let message = "The purchase couldn't be completed: \(error.localizedDescription)"
            storeMessage = message
            return message
        }
    }

    private func observeTransactions() -> Task<Void, Never> {
        Task(priority: .background) {
            for await result in Transaction.updates {
                guard case .verified(let transaction) = result else { continue }
                await self.refreshEntitlements()
                await transaction.finish()
            }
        }
    }

    private func refreshEntitlements() async {
        var currentIDs = Set<String>()
        var latestSubscriptionExpiration: Date?

        for await result in Transaction.currentEntitlements {
            guard case .verified(let transaction) = result else { continue }
            if transaction.revocationDate != nil { continue }

            if let expiration = transaction.expirationDate {
                if expiration < Date() { continue }
                if latestSubscriptionExpiration == nil || expiration > latestSubscriptionExpiration! {
                    latestSubscriptionExpiration = expiration
                }
            }

            currentIDs.insert(transaction.productID)
        }

        entitledProductIDs = currentIDs
        subscriptionExpiration = latestSubscriptionExpiration
        settingsStore.syncPurchaseState(
            entitledProductIDs: currentIDs,
            subscriptionExpiration: latestSubscriptionExpiration
        )
    }
}
