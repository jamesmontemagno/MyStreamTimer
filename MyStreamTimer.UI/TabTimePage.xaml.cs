using System;
using System.Collections.Generic;
using MyStreamTimer.Shared.Helpers;
using MyStreamTimer.Shared.Interfaces;
using MyStreamTimer.Shared.ViewModel;
using Xamarin.Forms;

namespace MyStreamTimer.UI
{
    public partial class TabTimePage : ContentPage
    {
        public TabTimePage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
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
    }
}
