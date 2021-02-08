using System;
using System.Collections.Generic;
using MyStreamTimer.Shared.ViewModel;
using Xamarin.Forms;

namespace MyStreamTimer.UI
{
    public partial class ProPage : ContentPage
    {
        ProViewModel vm;
        public ProPage()
        {
            InitializeComponent();
            BindingContext = vm = new ProViewModel();
        }

        protected override async void OnAppearing() => await vm.GoGetPrice();

        protected override void OnDisappearing() => vm.CancelGoGetPrice();
    }
}
