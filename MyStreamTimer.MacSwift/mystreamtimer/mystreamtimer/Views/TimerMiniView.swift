import SwiftUI

struct TimerMiniView: View {
    @EnvironmentObject private var appModel: AppModel
    let kind: TimerKind

    var body: some View {
        TimerMiniContent(
            controller: appModel.controller(for: kind),
            settingsStore: appModel.settingsStore
        )
    }
}

private struct TimerMiniContent: View {
    @ObservedObject var controller: TimerController
    @ObservedObject var settingsStore: LegacySettingsStore

    private var textColor: Color {
        Color(hex: settingsStore.popOutTextColorHex) ?? .white
    }

    private var backgroundColor: Color {
        Color(hex: settingsStore.popOutBackgroundColorHex) ?? .black
    }

    var body: some View {
        Text(controller.previewDisplayText)
            .font(settingsStore.popOutFont)
            .monospacedDigit()
            .foregroundStyle(textColor)
            .contentTransition(.numericText())
            .animation(.easeInOut(duration: 0.15), value: controller.previewDisplayText)
            .padding(20)
            .frame(minWidth: 360)
            .background(backgroundColor)
    }
}
