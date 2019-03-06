using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace Parakeet_3._0 {

    // singleton
    public sealed class ApplicationSettings {

        // names that can be changed
        #region settings name
        
        static readonly string debugMode = "debug mode";
        static readonly string tcpPort = "tcp port";
        static readonly string serverHost = "server host";

        #endregion

        #region default settings values

        public readonly string defaultPort = "13000";           // default port for TCP 
        public readonly string defaultHost = "localhost";       // default host name for TCP 
        public readonly bool defaultDebugMode = false;          // default debug mode value

        #endregion


        #region app settings management

        // debug mode option
        public bool DebugMode {
            set {
                AppLocalSettings.Values[debugMode] = value.ToString();
            }
            get {
                string boolValue = AppLocalSettings.Values[debugMode] as string;
                // check if the option exists. If not create and returns it
                if (String.IsNullOrWhiteSpace(boolValue)) {
                    DebugMode = false;      // adds the option [default: false]
                    return false;
                } else {
                    return boolValue.ToLower() == "true" ? true : false;      // return the option
                }
            }
        }
        
        // port for tcp connection
        public String ServerTCPPort {
            set {
                AppLocalSettings.Values[tcpPort] = value;
            }
            get {
                string port = AppLocalSettings.Values["tcp port"] as string;
                // check if the option exists. If not create and returns it
                if (String.IsNullOrWhiteSpace(port)) {
                    ServerTCPPort = defaultPort;
                    return defaultPort;
                } else {
                    return port;
                }
            }
        }

        // Host name for tcp connection
        public String ServerHostName {
            set {
                AppLocalSettings.Values[serverHost] = value;
            }
            get {
                string hostName = AppLocalSettings.Values[serverHost] as string;
                // check if the option exists. If not create and returns it
                if (String.IsNullOrWhiteSpace(hostName)) {
                        var hostNames = NetworkInformation.GetHostNames();
                    ServerHostName = hostNames.FirstOrDefault(name => name.Type == HostNameType.DomainName)?.DisplayName ?? defaultHost;
                    return defaultHost;
                } else {
                    return hostName;
                }
            }
        }   
        
        public void ResetSettings() {
            ServerHostName = defaultHost;
            ServerTCPPort = defaultPort;
            DebugMode = defaultDebugMode;
        }

        #endregion

        private ApplicationDataContainer AppLocalSettings { set; get; }
        private static ApplicationSettings instance;

        //static ApplicationSettings() {
        //    //Home.GetCurrent().AddLog("Settings Loaded", Models.AppLog.LogCategory.Debug);
        //}

        public static ApplicationSettings GetInstance {
            get {
                if (instance == null)
                    instance = new ApplicationSettings();
                
                return instance;
            }
        }

        private ApplicationSettings() {
            AppLocalSettings = ApplicationData.Current.LocalSettings;
            //Home.GetCurrent().AddLog("Settings file loaded.", Models.AppLog.LogCategory.Debug);
        }


    }
}
