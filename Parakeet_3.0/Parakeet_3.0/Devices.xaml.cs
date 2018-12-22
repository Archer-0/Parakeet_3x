using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Parakeet_3._0.Models;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

// Il modello di elemento Pagina vuota è documentato all'indirizzo https://go.microsoft.com/fwlink/?LinkId=234238

namespace Parakeet_3._0 {
    /// <summary>
    /// Pagina vuota che può essere usata autonomamente oppure per l'esplorazione all'interno di un frame.
    /// </summary>
    public sealed partial class Devices : Page {

        DeviceInformationCollection pairedDevices = null;
        public ObservableCollection<BTDevice> BTDevices { set; get; }

        public Devices() {
            this.InitializeComponent();
            BTDevices = new ObservableCollection<BTDevice>();
            CheckPairedDevices();

            //Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
            //    CoreDispatcherPriority.Normal, () => {
            //        CheckPairedDevices();
            //    }
            //);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            //CheckPairedDevices();
        }

        #region UI Elements Behavior

        // flyout menu on right click 
        private void DevicesGridView_RightTapped(object sender, RightTappedRoutedEventArgs e) {
            FlyoutMenu.ShowAt(sender as UIElement, e.GetPosition(sender as UIElement));
        }

        private void RefreshItem_Click(object sender, RoutedEventArgs e) {
            // when the user click on refresh the list of paired devices
            CheckPairedDevices();
        }

        private void StackPanel_Tapped(object sender, TappedRoutedEventArgs e) {
            BTDevice device = BTDevices[this.DevicesGridView.SelectedIndex];
            MainPage mainpg = MainPage.GetCurrent();
            mainpg.ChangeNavigationSelection(typeof(Home));
            this.Frame.Navigate(typeof(Home), device.DeviceInfo);
            
            Debug.WriteLine($"Selected device: {device.DeviceName} [{device.DeviceId}]");
        }

        #endregion

        #region Bluetooth Connection Logic

        private async void CheckPairedDevices() {
            Home.GetCurrent().AddLog("Checking paired devices...", AppLog.LogCategory.Debug);
            BTDevices.Clear();

            pairedDevices = await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelector());
            ObservableCollection<BTDevice> temp = new ObservableCollection<BTDevice>();

            foreach (var device in pairedDevices) {
                BTDevices.Add(new BTDevice(device));
                //Debug.WriteLine("Found device -" + device.Name + "(" + device.Id + ")");
                Home.GetCurrent().AddLog($"Device found: {device.Name} [{device.Id}]", AppLog.LogCategory.Debug);
            }
        }

        #endregion
    }
}
