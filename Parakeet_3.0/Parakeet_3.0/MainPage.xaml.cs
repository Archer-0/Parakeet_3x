/**
 * Thanks to github user "gdfonda" for the tutorial on fluent design (https://github.com/gdfonda/windows-developer-blog-samples/tree/master/Samples/WhatsPlaying)
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;


namespace Parakeet_3._0 {

    public sealed partial class MainPage : Page {

        public static MainPage mainPage;

        private readonly List<(string Tag, Type Page)> _pages = new List<(string Tag, Type Page)> {
            ("home", typeof(Home)),
            ("devices", typeof(Devices)),
            ("settings", typeof(Settings)),
            ("about", typeof(About))
        };

        private bool pageInitialized = false;

        public MainPage() {
            if (!pageInitialized) {
                this.InitializeComponent();
                mainPage = this;
                
                // set minimum size of the window (Only the MinHeight seems to work).
                ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size {
                    Height = App.preferredLaunchViewHeight,
                    Width = App.preferredLaunchViewWidth
                });

                // add the back button not displayer by default
                this.Loaded += MainPage_Loaded;

                // set the title in the custom title bar
                var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
                this.UpdateTitleBarLayout(coreTitleBar);
                Window.Current.SetTitleBar(this.AppTitleBar);
                coreTitleBar.LayoutMetricsChanged += (s, a) => UpdateTitleBarLayout(s);

                pageInitialized = true;
            }
        }

        public static MainPage GetCurrent() {
            //Frame appFrame = Window.Current.Content as Frame;
            //return appFrame.Content as MainPage;
            return mainPage;
        }


        // Change the page in the navigation pane to the given type of page
        public void ChangeNavigationSelection(Type page) {
            var tag = _pages.FirstOrDefault(p => p.Page.Equals(page)).Tag;
            int i = 0;
            foreach (NavigationViewItem item in MainContent.MenuItems) {
                if (item.Tag.ToString() == tag.ToString())
                    break;

                i++;
            }
            MainContent.SelectedItem = MainContent.MenuItems[i];
        }

        // things to do when the app start
        private void MainPage_Loaded(object sender, RoutedEventArgs e) {
            this.MainContent.IsPaneOpen = false;
            //ChangeNavigationSelection(typeof(Home));
            this.MainContent.SelectedItem = this.MainContent.MenuItems.First();
        }

        private void UpdateTitleBarLayout(CoreApplicationViewTitleBar coreTitleBar) {
            //var isVisible = SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility == AppViewBackButtonVisibility.Visible;

            //var width = isVisible ? coreTitleBar.SystemOverlayLeftInset : 0;
            var width = coreTitleBar.SystemOverlayLeftInset;

            LeftPaddingColumn.Width = new GridLength(width);

            // update title bar control size as needed to account for system size changes
            this.AppTitleBar.Height = coreTitleBar.Height;
        }

        // Manage the user input in the navigation view
        private void MainContent_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args) {

            if (args.IsSettingsSelected) {
                ContentFrame.Navigate(typeof(Settings), sender.Tag);

            } else {
                var item = args.SelectedItem as NavigationViewItem;
                var tag = item.Tag.ToString();

                Type _page = null;
                var thePage = _pages.FirstOrDefault(p => p.Tag.Equals(tag));
                _page = thePage.Page;
            
                ContentFrame.Navigate(_page, tag);
            }
        }

        private void NavigationViewItem_Tapped(object sender, TappedRoutedEventArgs e) {
            var item = sender as NavigationViewItem;
            var tag = item.Tag.ToString();

            Type _page = null;
            var thePage = _pages.FirstOrDefault(p => p.Tag.Equals(tag));
            _page = thePage.Page;

            ContentFrame.Navigate(_page, tag);
        }
    }
}
