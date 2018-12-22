using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Parakeet_3._0;

// Il modello di elemento Pagina vuota è documentato all'indirizzo https://go.microsoft.com/fwlink/?LinkId=234238

namespace Parakeet_3._0 {
    /// <summary>
    /// Pagina vuota che può essere usata autonomamente oppure per l'esplorazione all'interno di un frame.
    /// </summary>
    public sealed partial class Settings : Page {

        ApplicationSettings appSettings;

        public Settings() {


            this.InitializeComponent();

            appSettings = ApplicationSettings.GetInstance;

            // restore settings
            LoadSettings();

            DebugOptionCheckBox.Checked += OnAnySettingChanged;
            DebugOptionCheckBox.Unchecked += OnAnySettingChanged;
            TCPPortOptionText.TextChanged += OnAnySettingChanged;

            SettingsSaveButton.Content = "Save";
            SettingsSaveButton.IsEnabled = false;
        }

        private void OnAnySettingChanged(object sender, RoutedEventArgs e) {
            SettingsSaveButton.Content = "Save";
            SettingsSaveButton.IsEnabled = true;
        }

        private void SettingsSaveButton_Click(object sender, RoutedEventArgs e) {
            CheckOptionsAndSave();
            SettingsSaveButton.Content = "Saved";
            SettingsSaveButton.IsEnabled = false;
        }

        void CheckOptionsAndSave() {
            bool debugModeEnabled = (bool) DebugOptionCheckBox.IsChecked;
            appSettings.DebugMode = debugModeEnabled;

            Home.GetCurrent().AddLog("Debugmode: " + (bool)DebugOptionCheckBox.IsChecked, Models.AppLog.LogCategory.Debug);

            if (TCPPortOptionText.Text != "") {
                appSettings.ServerTCPPort = TCPPortOptionText.Text;
            }

            Home.GetCurrent().AddLog("Settings saved.", Models.AppLog.LogCategory.Debug);
        }

        void LoadSettings() {
            // from save file
            DebugOptionCheckBox.IsChecked = appSettings.DebugMode;
            TCPPortOptionText.Text = appSettings.ServerTCPPort;
            
        }

    }
}
