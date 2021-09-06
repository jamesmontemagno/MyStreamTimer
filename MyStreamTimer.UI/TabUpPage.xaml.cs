using System;
using System.Collections.Generic;
using MyStreamTimer.Shared.Helpers;
using MyStreamTimer.Shared.Interfaces;
using MyStreamTimer.Shared.ViewModel;
using Xamarin.Forms;

namespace MyStreamTimer.UI
{
    public partial class TabUpPage : ContentPage
    {
        public TabUpPage()
        {
            InitializeComponent();
        }

        protected async override void OnAppearing()
        {
            var vm = BindingContext as TimerViewModel;

            if (vm == null)
                return;

            if (vm.RequiresPro && !GlobalSettings.IsPro)
            {

                ProLabel.IsVisible = true;
                MainGrid.IsVisible = false;
                return;
            }

            ProLabel.IsVisible = false;
            MainGrid.IsVisible = true;
        }

        async void Picker_SelectedIndexChanged(System.Object sender, System.EventArgs e)
        {
            if (GlobalSettings.IsPro)
                return;

            var picker = (Picker)sender;
            if (picker.SelectedIndex == 0)
                return;
            await ServiceContainer.Resolve<IPlatformHelpers>().DisplayAlert("Pro Feature", "This is a Pro feature, head over to the Pro tab to upgrade today.");


            picker.SelectedIndex = 0;
        }
    }
}
