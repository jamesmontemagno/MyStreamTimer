import Combine
import Foundation
import OSLog
import StoreKit

@MainActor
final class PurchaseManager: ObservableObject {
    static let bronzeLifetimeID = "mstbronze"
    static let silverLifetimeID = "mstsilver"
    static let lifetimeID = "mstgold"
    static let oneMonthSubscriptionID = "mstsub"
    static let sixMonthSubscriptionID = "mstsub6months"
    static let lifetimeIDs: Set<String> = [bronzeLifetimeID, silverLifetimeID, lifetimeID]
    static let subscriptionIDs: Set<String> = [oneMonthSubscriptionID, sixMonthSubscriptionID]
    static let entitlementIDs = lifetimeIDs.union(subscriptionIDs)

    @Published private(set) var productsByID: [String: Product] = [:]
    @Published private(set) var entitledProductIDs: Set<String> = []
    @Published private(set) var isLoading = false
    @Published private(set) var storeMessage: String?
    @Published private(set) var subscriptionExpiration: Date?

    private let settingsStore: LegacySettingsStore
    private var updatesTask: Task<Void, Never>?
    private let logger = Logger(
        subsystem: Bundle.main.bundleIdentifier ?? "com.refractored.mystreamtimer",
        category: "Purchases"
    )

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
        return !entitledProductIDs.intersection(Self.lifetimeIDs).isEmpty
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
            let hadVerificationFailure = await refreshEntitlements(
                includeHistory: true,
                authoritative: true
            )
            let message: String
            if hadVerificationFailure {
                message = "Some purchase history couldn't be verified. Your legacy access was left unchanged; please try again or contact support."
            } else if entitledProductIDs.isEmpty {
                message = "No active purchases were found to restore."
            } else {
                message = "Your purchase status has been refreshed successfully."
            }
            storeMessage = message
            return message
        } catch {
            let message = "Unable to refresh purchase status: \(error.localizedDescription)"
            storeMessage = message
            return message
        }
    }

    func refreshPurchaseStatus() async {
        await refreshEntitlements(includeHistory: true)
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
                    if case .unverified(let transaction, let error) = verificationResult {
                        logger.error(
                            "Purchase verification failed for \(transaction.productID, privacy: .public): \(error.localizedDescription, privacy: .public)"
                        )
                    }
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
                switch result {
                case .verified(let transaction):
                    await self.refreshEntitlements()
                    await transaction.finish()
                case .unverified(let transaction, let error):
                    self.logger.error(
                        "Transaction update verification failed for \(transaction.productID, privacy: .public): \(error.localizedDescription, privacy: .public)"
                    )
                }
            }
        }
    }

    @discardableResult
    private func refreshEntitlements(
        includeHistory: Bool = false,
        authoritative: Bool = false
    ) async -> Bool {
        var currentIDs = Set<String>()
        var latestSubscriptionExpiration: Date?
        var hadVerificationFailure = false

        for await result in Transaction.currentEntitlements {
            switch result {
            case .verified(let transaction):
                apply(
                    transaction,
                    to: &currentIDs,
                    latestSubscriptionExpiration: &latestSubscriptionExpiration
                )
            case .unverified(let transaction, let error):
                hadVerificationFailure = true
                logger.error(
                    "Current entitlement verification failed for \(transaction.productID, privacy: .public): \(error.localizedDescription, privacy: .public)"
                )
            }
        }

        if includeHistory {
            for await result in Transaction.all {
                switch result {
                case .verified(let transaction):
                    apply(
                        transaction,
                        to: &currentIDs,
                        latestSubscriptionExpiration: &latestSubscriptionExpiration
                    )
                case .unverified(let transaction, let error):
                    hadVerificationFailure = true
                    logger.error(
                        "Transaction history verification failed for \(transaction.productID, privacy: .public): \(error.localizedDescription, privacy: .public)"
                    )
                }
            }
        }

        entitledProductIDs = currentIDs
        subscriptionExpiration = latestSubscriptionExpiration
        settingsStore.syncPurchaseState(
            entitledProductIDs: currentIDs,
            subscriptionExpiration: latestSubscriptionExpiration,
            authoritative: authoritative && !hadVerificationFailure
        )
        return hadVerificationFailure
    }

    private func apply(
        _ transaction: Transaction,
        to currentIDs: inout Set<String>,
        latestSubscriptionExpiration: inout Date?
    ) {
        guard Self.entitlementIDs.contains(transaction.productID) else { return }
        guard transaction.revocationDate == nil else { return }

        if let expiration = transaction.expirationDate {
            guard expiration >= Date() else { return }
            if latestSubscriptionExpiration == nil || expiration > latestSubscriptionExpiration! {
                latestSubscriptionExpiration = expiration
            }
        }

        currentIDs.insert(transaction.productID)
    }
}
