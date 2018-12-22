using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Parakeet_3._0.Models {

    public class AppLog {
        public DateTime TimeStamp { set; get; }
        public String LogText { set; get; }
        public LogCategory logCategory;
        public String GlyphIcon { set; get; }
        public Brush IconColor { set; get; }
        public string LogCategoryText { set; get; }

        private readonly String iconOpacity = "AA";

        public enum LogCategory {
            Error,
            Info,
            Warning,
            Debug
        }

        public AppLog(String logText, LogCategory logCategory) {
            TimeStamp = DateTime.Now;
            this.LogText = logText;
            this.logCategory = logCategory;
            SetLogProps(this.logCategory);
        }

        // only for debug purposes
        public AppLog() {
            TimeStamp = DateTime.Now;
            this.LogText = "Dummy Log";
            this.logCategory = LogCategory.Debug;
            SetLogProps(this.logCategory);
        }


        // icons codes from https://docs.microsoft.com/it-it/windows/uwp/design/style/segoe-ui-symbol-font
        private void SetLogProps(LogCategory cat) {
            IconColor = new SolidColorBrush();
            switch (cat) {
                case LogCategory.Debug:
                    this.LogCategoryText = "[Debug]";
                    this.GlyphIcon = "\uEBE8";                                              // Bug icon
                    this.IconColor = GetSolidColorBrush("#" + iconOpacity + "8E44AD");      // purple
                    break;
                case LogCategory.Error:
                    this.LogCategoryText = "[Error]";
                    this.GlyphIcon = "\uE783";                                              // Error icon
                    this.IconColor = GetSolidColorBrush("#" + iconOpacity + "D60000");      // red
                    break;
                case LogCategory.Info:
                    this.LogCategoryText = "[Info]";
                    this.GlyphIcon = "\uE946";                                              // Info icon ( change to e946 for info icon / e8bd for message icon)
                    this.IconColor = GetSolidColorBrush("#" + iconOpacity + "FFFFFF");      // white
                    break;
                case LogCategory.Warning:
                    this.LogCategoryText = "[Warning]";
                    this.GlyphIcon = "\uE7BA";                                              // Warning icon
                    this.IconColor = GetSolidColorBrush("#" + iconOpacity + "FFE900");      // yellow
                    break;
                default:
                    this.LogCategoryText = "[Uncategorized]";
                    this.GlyphIcon = "\uE8BD";                                              // Message icon
                    this.IconColor = GetSolidColorBrush("#" + iconOpacity + "FFFFFF");      // white
                    break;
            }
        }


        // thank to "Joel Joseph" for this method (http://joeljoseph.net/converting-hex-to-color-in-universal-windows-platform-uwp/)
        public Brush GetSolidColorBrush(string hex) {
            hex = hex.Replace("#", string.Empty);
            byte a = (byte) (Convert.ToUInt32(hex.Substring(0, 2), 16));
            byte r = (byte) (Convert.ToUInt32(hex.Substring(2, 2), 16));
            byte g = (byte) (Convert.ToUInt32(hex.Substring(4, 2), 16));
            byte b = (byte) (Convert.ToUInt32(hex.Substring(6, 2), 16));
            Brush myBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(a, r, g, b));
            return myBrush;
        }
    }
}
