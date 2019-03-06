using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using MiBand2SDK;
using Microsoft.Toolkit.Uwp.Notifications;
using Parakeet_3._0.Models;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.ExtendedExecution.Foreground;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Parakeet_3._0 {

    public sealed partial class Home : Page {

        readonly Guid HRserviceGuid = new Guid("0000180d-0000-1000-8000-00805f9b34fb");             //Heart rate service's GUID
        readonly Guid HRCharacteristicGuid = new Guid("00002A37-0000-1000-8000-00805F9B34FB");      //Heart rate monitor characteristic's GUID

        bool DeviceConnected { get; set; } = false;                         // connection status of the HR device
        GattDeviceService HRReaderService { get; set; } = null;             // service that provides the HR reader
        GattCharacteristic HRReaderCharacteristic { get; set; } = null;     // characteristic of the HR reader and HR data
        DeviceInformation ChosenDevice { get; set; } = null;                // the chosen device for connection
        BluetoothLEDevice BluetoothDevice { set; get; } = null;             // bluetooth informations of the chosen device

        private Radio bluetoothRadio = null;                                // link to the bluetooth device of this device

        private bool prevBluetoothState = true;
        private bool isBluetoothEnabled = false;
        private bool IsBluetoothEnabled {
            get {
                return this.isBluetoothEnabled;
            }

            set {
                prevBluetoothState = this.isBluetoothEnabled;
                this.isBluetoothEnabled = value;
            }

        }

        private bool IsMiBand2 { get; set; } = false;        // check if the device is a mi band (requires a special treatment)

        DispatcherTimer HrTimerController { get; set; } = null;                  // timer for mi band HR timeout (workaround for a porblem with mi band connection)
        DateTimeOffset startTime;                           // when the timer started
        DateTimeOffset lastTime;                            // last check time

        DispatcherTimer DispatcherTimer { get; set; } = null;

        Stream InStream { get; set; } = null;                   // TCP server input stream
        StreamReader InStreamReader { get; set; } = null;       // reader fot input TCP stream

        MiBand2 miBand = null;                                  // Mi Band device (MIBand2SDK)
        bool ServerConnected { get; set; } = false;                                 // connection status of the tcp server
        public string ServerPort { get; set; } = "13000";                           // server port [default: "13000"] (can be changed from settings)
        StreamSocket StreamSocket { get; set; } = null;                             // TCP client socket to connect to the server
        public HostName ServerHost { get; set; } = null;       // this host name [default: "localhost"] (can be changed from settings)

        public string BPMValue { get; private set; }        // last bpm value

        private static Home thisHome = new Home();      // instance of this class

        public ObservableCollection<AppLog> logList;        // list of logs for the loglist in the UI

        public static MainPage mainPage;        // a reference to the main page class

        bool initialized = false;       // to not initialize this class every time

        ApplicationSettings appSettings;

        public Home() {
            if (!initialized) {
                this.InitializeComponent();
                logList = new ObservableCollection<AppLog>();
                mainPage = MainPage.GetCurrent();
                thisHome = this;
                appSettings = ApplicationSettings.GetInstance;

                AddLog("Application started. initializing.", AppLog.LogCategory.Debug);
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;

                BPMValue = "0";

                Initialize();
            }
        }

        // get current instance of this class
        public static Home GetCurrent() {
            //Frame appFrame = Window.Current.Content as Frame;
            //return appFrame.Content as Home;
            if (thisHome == null)
                thisHome = new Home();
            return thisHome;
        }

        // set the chosen device for the connection
        protected override void OnNavigatedTo(NavigationEventArgs e) {
            if (e.Parameter is DeviceInformation device) {
                SetNewDevice(device);
            }
        }

        // set new device
        private async void SetNewDevice(DeviceInformation device) {
            await DisconnectDevice();

            ChosenDevice = device;      // set chosen device in the public variable

            this.ConnectStatusText.Text = "Selected device:\n" + "\"" + device.Name + "\"";      // change info text with the name of the chosen device


            // set tooltip to the infotext to show the id of the connected 
            ToolTipService.SetToolTip(this.ConnectStatusText, new ToolTip { Content = ChosenDevice.Name + "\n[id: " + ChosenDevice.Id.ToString() + "]"});

            // change hyper link button text (pure estesic purposes)
            this.DevicesPageLink.Content = "Change device";
            AddLog("Device selected: " + ChosenDevice.Name, AppLog.LogCategory.Debug);

            // check if the device is a mi band in order to use an alternative connection method
            IsMiBand2 = false;
            if (ChosenDevice.Name.ToLower().Contains("mi band 2")) {
                AddLog("Mi Band 2 detected, switched to alternative connection method.", AppLog.LogCategory.Debug);
                IsMiBand2 = true;
                miBand = new MiBand2();
            }

            ListCurrentDeviceServicesToLog();        // list all available services of the chosen device in the loglist (debug purposes)
            await CheckBluetoothStatus(true);     // check bluetooth state and if it's turned off, try to turn it on
            
            // enable the connect button and set a tooltip
            this.ConnectButton.IsEnabled = true;
            ToolTipService.SetToolTip(this.ConnectButton, new ToolTip { Content = "Press this button to connect to the choosen device" });
        }

        // init the application to the initial state
        private async void Initialize() {
            await GetBluetoothDevice();     // get the bluetooth adapter and set the public variable
            InitializeUIComponents();       // set UI components
            await CheckBluetoothStatus(true);     // check bluetooth status and if off, try to turn it on
            
            //AddBluetoothStateEventListener();       // adds a eventlistener to the bluetooth state
        }

        #region log functions

        public void AddLog(String logText, AppLog.LogCategory logCategory) {
            AppLog log = null;
            log = new AppLog(logText, logCategory);

            switch (logCategory) {
                // if debug mode is not active and the log category is Debug
                case AppLog.LogCategory.Debug when !appSettings.DebugMode:
                    break;
                default:
                    logList.Insert(0, log);
                    break;
                    //log = new AppLog(logText, logCategory);
            }

            // TODO: write only on file
            Debug.WriteLine(log.LogCategoryText + " " + log.LogText);
        }

        private void ClearLogFlyItem_Click(object sender, RoutedEventArgs e) {
            logList.Clear();
        }

        private void SaveLogFlyItem_Click(object sender, RoutedEventArgs e) {

        }

        #endregion

        #region UI Elements Functions

        delegate void DelegateFunc();
        // initialize home interface components
        private void InitializeUIComponents() {
            // set infotext 
            ConnectStatusText.Text = "No device selected.";

            // disable the connect button 
            this.ConnectButton.Content = "Connect to device";
            this.ConnectButton.Click -= DisconnectButton_Click1;
            this.ConnectButton.Click -= ConnectButton_Click;
            this.ConnectButton.Click += ConnectButton_Click;
            this.ConnectButton.IsEnabled = false;

            // set hiper link button text
            this.DevicesPageLink.Content = "Select device to start";
            BPMText.Visibility = Visibility.Collapsed;
            this.MiBand2DebugMenu.Visibility = Visibility.Collapsed;        // debug menu for mi band 2
        }


        // manage the behavior connect button 
        private void ConnectButton_Click(object sender, RoutedEventArgs e) {

            // if the the device has been already choosen start the connection process
            if (ChosenDevice != null) {
                //AddLog("Connecting to " + ChosenDevice.Name + "...", AppLog.LogCategory.Info);

                // logic for connection
                if (IsMiBand2)
                    ConnectMiBand();
                else
                    ConnectGenericDevice();

                BPMText.Visibility = Visibility.Visible;

            } else {
                AddLog("No device selected. Select a device from the list in Devices Page", AppLog.LogCategory.Warning);
                var button = sender as Button;
                button.IsEnabled = false;
            }
        }

        void SwitchConnectButtonToDisconnectButton() {
            ConnectButton.Content = "Disconnect from device";
            ToolTipService.SetToolTip(ConnectButton, new ToolTip { Content = $"Press here to disconnect from {ChosenDevice.Name}."});
            this.ConnectButton.Click -= ConnectButton_Click;
            ConnectButton.Click += DisconnectButton_Click1;
        }

        private async void DisconnectButton_Click1(object sender, RoutedEventArgs e) {
            await DisconnectDevice();
            InitializeUIComponents();
        }

        #region Mi Band debug
        private void GetHRMeasurementMiBandButton_Click(object sender, RoutedEventArgs e) {
            MiBandGetHRMeasurement();
        }

        private async void DisconnectMiBandButton_Click(object sender, RoutedEventArgs e) {
            await DisconnectDevice();
        }
        #endregion

        // redirect the user to the devices page
        private void DevicesPageHyperlinkButton_Click(object sender, RoutedEventArgs e) {
            MainPage mainpg = MainPage.GetCurrent();
            mainpg.ChangeNavigationSelection(typeof(Devices));
            this.Frame.Navigate(typeof(Devices));
        }

        // show a flyout menu under under the pointer
        private void ShowFlyOutMenu(object sender, RightTappedRoutedEventArgs e) {
            FlyoutMenu.ShowAt(sender as UIElement, e.GetPosition(sender as UIElement));
        }

        #endregion

        #region Bluetooth Comunication Functions

        private async Task GetBluetoothDevice() {
            var result = await Radio.RequestAccessAsync();      // request access to radio devices 

            // if access to the radio devices is allowed
            if (result == RadioAccessStatus.Allowed) {
                AddLog($"Radio access status: {result.ToString()}", AppLog.LogCategory.Debug);

                var bluetooth = (await Radio.GetRadiosAsync()).FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);      // try to get the bluetooth device

                // add event handler
                if (bluetooth != null) {
                    this.bluetoothRadio = bluetooth;
                    AddLog($"{bluetoothRadio.Name} obtained.", AppLog.LogCategory.Debug);
                } else {
                    AddLog($"Radio access status: {result.ToString()}, returning.", AppLog.LogCategory.Debug);
                    AddLog("Can not access communicate with the bluetooth adapter of this device.", AppLog.LogCategory.Error);
                    return;
                }
            }
        }

        private void RemoveBluetoothStateEventListener() {
            if (bluetoothRadio != null) {
                this.bluetoothRadio.StateChanged -= Bluetooth_StateChanged;
            }
        }

        private void AddBluetoothStateEventListener() {
            if (bluetoothRadio != null) {
                //this.bluetoothRadio.StateChanged -= Bluetooth_StateChanged;
                this.bluetoothRadio.StateChanged += Bluetooth_StateChanged;

            }
        }

        private async Task CheckBluetoothStatus(bool activateIfOff) {

            if (bluetoothRadio != null && bluetoothRadio.State != RadioState.On) {

                // if bluetooth is off and "activateIfOff" == true , try to activate it
                if (activateIfOff) {
                    AddLog($"Bluetooth state: {bluetoothRadio.State.ToString()}, trying to turn it on...", AppLog.LogCategory.Debug);

                    RadioAccessStatus radioAccess = RadioAccessStatus.Unspecified;

                    try {
                        radioAccess = await bluetoothRadio.SetStateAsync(RadioState.On);       // try to turn on the bluetooth adapter

                    } catch (Exception e) {
                        AddLog($"Can't modify state of {bluetoothRadio.Name}. \nMessage: {e.Message}", AppLog.LogCategory.Debug);
                        return;
                    }

                    // if the operation finished sucessfully change the bool variable
                    if (radioAccess == RadioAccessStatus.Allowed && bluetoothRadio.State == RadioState.On) {
                        AddLog($"Bluetooth activated. New state: {bluetoothRadio.State.ToString()}.", AppLog.LogCategory.Debug);
                        IsBluetoothEnabled = true;

                    } else {
                        AddLog($"Something gone wrong. New bluetooth state: {bluetoothRadio.State.ToString()}.", AppLog.LogCategory.Debug);
                        IsBluetoothEnabled = false;
                    }

                    // if activateIfOff == false, just change the state of the variable
                } else {
                    AddLog($"Bluetooth state: {bluetoothRadio.State.ToString()}", AppLog.LogCategory.Debug);
                    IsBluetoothEnabled = false;
                }

                // bluetooth == null means that there is no access to the bluetooth device
            } else if (bluetoothRadio == null) {
                AddLog("Can not comunicate with bluetooth.", AppLog.LogCategory.Error);
                IsBluetoothEnabled = false;
                return;
                // if radio state == on ht ebluetooth is already on
            } else if (bluetoothRadio != null && bluetoothRadio.State == RadioState.On) {
                AddLog($"Bluetooth state: {bluetoothRadio.State.ToString()}", AppLog.LogCategory.Debug);
                IsBluetoothEnabled = true;
            }

        }

        // don't know why but this is being triggered twice every events
        private async void Bluetooth_StateChanged(Radio sender, object args) {

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {

                GetCurrent().AddLog($"[{sender.Kind.ToString()}] state changed. New state: {sender.State}.", AppLog.LogCategory.Debug);

            });

            sender.StateChanged -= Bluetooth_StateChanged;
            sender.StateChanged += Bluetooth_StateChanged;
        }

        private async void ConnectGenericDevice() {

            if (ChosenDevice != null) {

                await CheckBluetoothStatus(true);     // check bluetooth status and activate it if is turned off
                if (!IsBluetoothEnabled) {
                    AddLog("Can not connect with bluetooth disabled. Turn on the Bluetooth and try again.", AppLog.LogCategory.Warning);
                    return;
                }

                // request access to the selected device
                BluetoothDevice = await BluetoothLEDevice.FromIdAsync(ChosenDevice.Id);
                DeviceAccessStatus accessStatus = await BluetoothDevice.RequestAccessAsync();

                // log
                AddLog("[Connection: " + accessStatus.ToString() + "]" + " Connecting to " + BluetoothDevice.Name + "...", AppLog.LogCategory.Debug);
                AddLog("Connecting to " + BluetoothDevice.Name + "...", AppLog.LogCategory.Info);

                GattCharacteristicsResult hrGattCHaracteristics = null;

                // try to read the device charateristics
                try {
                    var gattDeviceServicesResult = await BluetoothDevice.GetGattServicesForUuidAsync(HRserviceGuid);        // get services with the HR service GUID

                    // for each service withe the given GUID try to read get 
                    foreach (GattDeviceService service in gattDeviceServicesResult.Services) {

                        AddLog("[" + ChosenDevice.Name + "] Found service. " +
                            "\n - Handle: " + service.AttributeHandle.ToString() +
                            "\n - UUID: " + service.Uuid.ToString(), AppLog.LogCategory.Debug);       // log

                        if (await service.GetCharacteristicsForUuidAsync(HRCharacteristicGuid) != null) {
                            hrGattCHaracteristics = await service.GetCharacteristicsForUuidAsync(HRCharacteristicGuid);
                            HRReaderService = service;
                            break;
                        }
                    }

                } catch {

                    AddLog("Device \"" + ChosenDevice.Name + "\" does not support HR service." +
                        "\nSelect another one from the devices list.", AppLog.LogCategory.Warning);

                    return;
                }

                // get the HR reader characteristic
                if (hrGattCHaracteristics != null) {
                    foreach (GattCharacteristic characteristic in hrGattCHaracteristics.Characteristics.Where(c => c.Uuid.Equals(HRCharacteristicGuid))) {
                        HRReaderCharacteristic = characteristic;
                    }
                } else {
                    // log something
                    return;
                }

                // if HR characteristic can't be found, show an error and return
                if (HRReaderCharacteristic == null) {
                    AddLog("Heart rate monitor characteristic NOT found.", AppLog.LogCategory.Debug);
                    AddLog("Could not connect to Heart Rate service of the device \"" + ChosenDevice.Name + "\".", AppLog.LogCategory.Warning);
                    return;
                }
                // if HR characteristic have been found, then start reading process
                else {
                    AddLog("Heart rate monitor characteristic found [Handle: " +
                        HRReaderCharacteristic.AttributeHandle.ToString() + "]", AppLog.LogCategory.Debug);

                    BeginBluetoothReadProcess();

                    SwitchConnectButtonToDisconnectButton();
                }

            } else {
                AddLog("The button should be disabled. Kowalski analisis.", AppLog.LogCategory.Debug);
                return;
            }
        }

        private async Task DisconnectDevice() {

            if (ChosenDevice != null) {

                DeviceInformation delDev = ChosenDevice;
                if (IsMiBand2 && miBand != null) {
                    string bandName = ChosenDevice.Name;
                    AddLog($"Disconnecting from {bandName}...", AppLog.LogCategory.Debug);

                    if (miBand.IsConnected()) {
                        try {
                            await miBand.HeartRate.UnsubscribeFromHeartRateNotificationsAsync(MiBandHRValueChanged);
                            await miBand.HeartRate.SetRealtimeHeartRateMeasurement(MiBand2SDK.Enums.RealtimeHeartRateMeasurements.DISABLE);
                        } catch {
                            AddLog($"Device not connected.", AppLog.LogCategory.Debug);
                        }
                    }

                    miBand = null;
                    IsMiBand2 = false;

                } else {
                    AddLog($"Disconnectiong from {ChosenDevice.Name}...", AppLog.LogCategory.Info);
                    try {
                        await HRReaderCharacteristic.
                            WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);      // notify the device the deisconnection
                        HRReaderCharacteristic.Service.Dispose();
                        HRReaderService.Dispose();
                        BluetoothDevice.Dispose();
                    } catch {
                        AddLog("Device comunication problem. Can't send disconnect signal.", AppLog.LogCategory.Debug);
                    }
                }

                DispatcherTimer.Stop();
                HrTimerController.Stop();

                ChosenDevice = null;
                InitializeUIComponents();
                AddLog($"{delDev.Name} sucessfully disconnected.", AppLog.LogCategory.Info);

            } else {
                AddLog($"There is no connected device.", AppLog.LogCategory.Info);
                return;
            }
        }

        private async void ConnectMiBand() {

            string message = "Please, remember that the Mi Band 2 support is still in development. We are not responable for any damage.\nUse this software at your own risk!";
            ContentDialog betaDialog = new ContentDialog {
                Title = "Mi Band 2 beta support.",
                Content = message,
                CloseButtonText = "Got it",
            };

            await betaDialog.ShowAsync();

            AddLog($"Connecting to {ChosenDevice.Name}. Thouch the band if any message appears.", AppLog.LogCategory.Info);     // log
            
            if (await miBand.ConnectAsync(ChosenDevice) && await miBand.Identity.AuthenticateAsync()) {

                if (appSettings.DebugMode)
                    this.MiBand2DebugMenu.Visibility = Visibility.Visible;

                AddLog($"{await miBand.Device.GetDeviceName()} Info: " +
                    $"\n" + 
                    $"\n----- BATTERY -----" +
                    $"\n--> battery level: \t\t{await miBand.Battery.GetBatteryChargeLevel()}%" +
                    $"\n--> last charge: \t\t{(await miBand.Battery.GetLastChargingDate()).ToString()}" +
                    $"\n--> charge cycles: \t{(await miBand.Battery.GetTotalChargeCycles())}" +
                    $"\n--> is charging: \t\t{(await miBand.Battery.IsDeviceCharging()).ToString()}" +
                    $"\n" +
                    $"\n----- HARDWARE/SOFTWARE INFO -----" +
                    $"\n--> hardware revision: \t{await miBand.Device.GetHardwareRevision()}" +
                    $"\n--> serial number: \t{await miBand.Device.GetSerialNumber()}" +
                    $"\n--> software revision: \t{await miBand.Device.GetSoftwareRevision()}" +
                    $"\n" +
                    $"\n----- HEARTRATE DATA -----" +
                    $"\n--> last heart rate measurement data: \t{miBand.HeartRate.LastHeartRate} BPM"
                    , AppLog.LogCategory.Debug);

                await MiBandVibrate();        // notify to user that the mi band is connected

                DeviceConnected = true;     // change connection status
                DispatcherTimerSetup();

                await MiBandGetHRMeasurementRealTime();

                UpdateBPMTextThread();

                SwitchConnectButtonToDisconnectButton();

            } else {
                AddLog($"Unable to connect to {ChosenDevice.Name}." +
                    $"\nVerify if it's nearby and it's turned on.", AppLog.LogCategory.Warning);
            }
        }

        private async void UpdateBPMTextThread () {
            await Task.Factory.StartNew(async () => {

                // horrible method, but works for now
                while (true) {

                    BPMValue = miBand.HeartRate.LastHeartRate.ToString();

                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.High, () => {
                        //Home.GetCurrent().AddLog($"BPM: {miBand.HeartRate.LastHeartRate}", Models.AppLog.LogCategory.Info);
                        if (Home.GetCurrent().CurrentBPMText.Visibility != Visibility.Visible) {
                            Home.GetCurrent().CurrentBPMText.Visibility = Visibility.Visible;
                        }

                        // set bpm value to share via tcp connections
                        Home.GetCurrent().CurrentBPMText.Text = $"{BPMValue} BPM";
                        //Home.GetCurrent().CurrentBPMText.Text = $"{miBand.HeartRate.LastHeartRate} BPM";
                        //Home.GetCurrent().BPMValue = $"{miBand.HeartRate.LastHeartRate}"; 
                    });

                    Thread.Sleep(1000);
                }
            });
        }

        private async Task MiBandVibrate() {
            await miBand.Notifications.SendDefaultNotification(MiBand2SDK.Enums.NotificationType.VIBRATE_ONLY);
        }

        async void MiBandHRValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args) {
            
            try {
                await miBand.HeartRate.SetRealtimeHeartRateMeasurement(MiBand2SDK.Enums.RealtimeHeartRateMeasurements.ENABLE);
            } catch {
                Debug.WriteLine("Device not connected");
                //return;
            }

            int currentHeartRate = args.CharacteristicValue.ToArray()[1];
            lastTime = DateTimeOffset.Now;
        }

        private async void HrTimerController_Tick(object sender, object e) {
            var span = DateTimeOffset.Now - lastTime;
            if (span > new TimeSpan(0, 0, 5)) {
                try {
                    //await miBand.HeartRate.UnsubscribeFromHeartRateNotificationsAsync(MiBandHRValueChanged);
                    //await miBand.HeartRate.SubscribeToHeartRateNotificationsAsync(MiBandHRValueChanged);
                    //await miBand.HeartRate.SetRealtimeHeartRateMeasurement(MiBand2SDK.Enums.RealtimeHeartRateMeasurements.ENABLE);
                    await miBand.HeartRate.GetHeartRateAsync();
                    lastTime = DateTimeOffset.Now;

                } catch(Exception ex) {
                    AddLog($"Exception while resetting hr timer. {ex.StackTrace}", AppLog.LogCategory.Debug);
                    return;
                }
                //AddLog($"HR Time Out. Resetting...", AppLog.LogCategory.Debug);
                Debug.WriteLine("HR Time Out. Resetting");
            }
        }

        private async Task MiBandGetHRMeasurementRealTime() {
            AddLog($"[2]Connection status: {miBand.IsConnected()}" +
                 $"\nAuth status: {miBand.Identity.IsAuthenticated()}", AppLog.LogCategory.Debug);

            bool connected = miBand.IsConnected();
            if (connected && miBand.Identity.IsAuthenticated()) {

                GattCommunicationStatus status = await miBand.HeartRate.SetRealtimeHeartRateMeasurement(MiBand2SDK.Enums.RealtimeHeartRateMeasurements.ENABLE);

                AddLog($"Real time measurement status: {connected.ToString()}", AppLog.LogCategory.Debug);
                

                if (connected) {
                    AddLog($"{ChosenDevice.Name} connected. Waiting for heart rate data...", AppLog.LogCategory.Debug);
                    //await miBand.HeartRate.GetHeartRateAsync();
                    
                    // setting up timer for HR measurements timeout
                    HrTimerController = new DispatcherTimer();
                    HrTimerController.Tick += HrTimerController_Tick;
                    HrTimerController.Interval = new TimeSpan(0, 0, 0, 0, 500);

                    startTime = DateTimeOffset.Now;
                    lastTime = startTime;
                    //HrTimerController.Start();
                    await Task.Run(async () => {
                        await miBand.HeartRate.SubscribeToHeartRateNotificationsAsync(MiBandHRValueChanged);
                    });

                    // loop for the measurements
                    await Task.Factory.StartNew(async () => {
                        await miBand.HeartRate.GetHeartRateAsync();
                    });

                }
            }
        }

        private async void ReadSession_Revoked(object sender, ExtendedExecutionForegroundRevokedEventArgs args) {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.High, () => {
                    Home.GetCurrent().AddLog($"Extended execution session revoked. Closing server.", Models.AppLog.LogCategory.Debug);
                });
        }

        //public async void RegisterAndRunAsync(DeviceInformation devInfo) {
        //    var taskName = typeof(BackgroundTasks.CheckHeartRateInBackgroundTask).ToString();

        //    IBackgroundTaskRegistration checkHeartRateInBackground = BackgroundTaskRegistration.AllTasks.Values.FirstOrDefault(t => t.Name == taskName);

        //    if (miBand.IsConnected() && miBand.Identity.IsAuthenticated()) {

        //        if (checkHeartRateInBackground != null) {
        //            // if the task is already executing, unregister it
        //            checkHeartRateInBackground.Unregister(true);
        //        }

        //        AddLog("Requesting permission to execute background process...", AppLog.LogCategory.Debug);
        //        await BackgroundExecutionManager.RequestAccessAsync();
        //        AddLog("Permission to execute background process obtained.", AppLog.LogCategory.Debug);

        //        AddLog($"Building new task...", AppLog.LogCategory.Debug);
        //        var devTrigger = new DeviceUseTrigger();
        //        var taskBuilder = new BackgroundTaskBuilder {
        //            Name = taskName,
        //            TaskEntryPoint = typeof(BackgroundTasks.CheckHeartRateInBackgroundTask).ToString(),
        //            IsNetworkRequested = false
        //        };

        //        taskBuilder.SetTrigger(devTrigger);
        //        AddLog($"Task [{taskBuilder.Name}] builded.", AppLog.LogCategory.Debug);

        //        AddLog($"Registering new task[{taskBuilder.Name}]", AppLog.LogCategory.Debug);

        //        BackgroundTaskRegistration task = taskBuilder.Register();

        //        AddLog($"Task [{taskBuilder.Name}] registered.", AppLog.LogCategory.Debug);

        //        task.Completed += Task_Completed;
        //        task.Progress += Task_Progress;

        //        AddLog($"Activating background activity on device [{devInfo.Name}, {devInfo.Id}]", AppLog.LogCategory.Debug);
                
        //        DeviceTriggerResult x = await devTrigger.RequestAsync(devInfo.Id);
        
        //        AddLog($"Activation status: {x.ToString()}", AppLog.LogCategory.Debug);
        //    }
        //}

        private static void Task_Progress(BackgroundTaskRegistration sender, BackgroundTaskProgressEventArgs args) {
            Debug.WriteLine($"Task {sender.Name} progress: {args.Progress.ToString()}");
        }

        private static void Task_Completed(BackgroundTaskRegistration sender, BackgroundTaskCompletedEventArgs args) {
            Debug.WriteLine($"Task \"{sender.Name}\" finished ({args.ToString()})");
        }

        private async void MiBandGetHRMeasurement() {
            if (IsMiBand2 && miBand != null && miBand.IsConnected()) {
                AddLog($"[{await miBand.Device.GetDeviceName()}] reading...", AppLog.LogCategory.Info);
                AddLog($"HR measurement: {await Task.Run(()=> miBand.HeartRate.GetHeartRateAsync())} BPM", AppLog.LogCategory.Info);
            }
        }

        async void BeginBluetoothReadProcess() {
            GattCommunicationStatus communicationStatus = GattCommunicationStatus.ProtocolError;

            try {
                AddLog("Sending notify request to device", AppLog.LogCategory.Debug);
                communicationStatus = await HRReaderCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Notify);

            } catch (Exception e) {
                // the device refuses the notification request
                AddLog("Request refused by device. Please retry.", AppLog.LogCategory.Warning);
                AddLog($"Exception StackTrace: \n+ {e.StackTrace}\nMessage: {e.Message}", AppLog.LogCategory.Debug);
            }

            DeviceConnected = false;

            switch (communicationStatus) {
                case GattCommunicationStatus.Success: {
                    AddLog($"Notify request activated. " +
                        $"\n[Gatt Comunication Status {communicationStatus.ToString()}", AppLog.LogCategory.Debug);      // log

                    DeviceConnected = true;     // change connection status

                    DispatcherTimerSetup();
                    ReadFromDevice();
                    break;
                }
                default: {
                    AddLog($"Notify request error" +
                        $"\n[Gatt Comunication Status {communicationStatus.ToString()}", AppLog.LogCategory.Debug);      // log
                    break;
                }
            }
        }

        private void ReadFromDevice() {
            HRReaderCharacteristic.ValueChanged += HRReaderCharacteristic_ValueChanged;
        }

        private async void HRReaderCharacteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args) {
            int arrayLenght = (int) args.CharacteristicValue.Length;
            byte[] rawHRData = new byte[arrayLenght];

            DataReader.FromBuffer(args.CharacteristicValue).ReadBytes(rawHRData);

            var hrValue = ProcessData(rawHRData);
            BPMValue = hrValue.ToString();

            // dispaly the bpm value somewhere
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal, () => {
                    AddLog("BPM: " + hrValue.ToString(), AppLog.LogCategory.Info);
                });
        }

        // translate the raw data coming from the devie
        private int ProcessData(Byte[] data) {
            // Heart rate profile defined flag values
            const byte heartRateValueFormat = 0x01;
            byte currentOffset = 0;
            byte flags = data[currentOffset];
            bool isHeartRateValueSizeLong = (flags & heartRateValueFormat) != 0;        // true if 16 bit format / false if 8 bit format
            string size = isHeartRateValueSizeLong ? "16 bit" : "8 bit";

            AddLog($"HR value Size: {size}", AppLog.LogCategory.Debug);

            currentOffset++;

            ushort heartRateMeasurementValue;

            //16 bit
            if (isHeartRateValueSizeLong) {
                heartRateMeasurementValue = (ushort) ((data[currentOffset + 1] << 8) + data[currentOffset]);
                currentOffset += 2;
            }
            //8 bit
            else {
                heartRateMeasurementValue = data[currentOffset];
            }
            return heartRateMeasurementValue;
        }

        public async void ListCurrentDeviceServicesToLog() {
            //CheckBluetoothStatus(true);
            BluetoothLEDevice device = await BluetoothLEDevice.FromIdAsync(ChosenDevice.Id);
            if (device == null) {
                return;
            }
            DeviceAccessStatus accessStatus = await device.RequestAccessAsync();
            var deviceServicesResult = await device.GetGattServicesAsync();

            // build the string to add in the log
            string services = "[Services list of " + device.Name + "]";
            int i = 0;
            foreach (GattDeviceService service in deviceServicesResult.Services) {
                services += "\n -- Handle: \t" + service.AttributeHandle.ToString();
                services += "\n --> UUID: \t" + service.Uuid.ToString();
                services += "\n --> Access Info: \t" + service.DeviceAccessInformation.CurrentStatus.ToString();
                i++;
                if (i < deviceServicesResult.Services.Count)
                    services += "\n----------------------------------------------------------------";
            }
            services += "\n\nFor info on Bluetooth Standard codes and identifiers, please visit https://www.bluetooth.com/specifications/assigned-numbers" + "\n";
            // add log
            AddLog(services, AppLog.LogCategory.Debug);
        }

        // ping the device to verify if is still reachable
        private async void PingDevice() {
            GattCommunicationStatus status = GattCommunicationStatus.ProtocolError;
            // repeat the notify request
            if (IsMiBand2) {
                status = await miBand.HeartRate.SetRealtimeHeartRateMeasurement(MiBand2SDK.Enums.RealtimeHeartRateMeasurements.ENABLE);
                
            } else {
                status = await HRReaderCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
            }

            if (status == GattCommunicationStatus.Success) {
                if (DeviceConnected == false) {
                    DeviceConnected = true;//the device is reachable
                    AddLog("Device \"" + ChosenDevice.Name + "\" is reachable again.", AppLog.LogCategory.Info);
                }
            } else {
                if (DeviceConnected == true) {
                    DeviceConnected = false; //the device is not reachable
                    AddLog("Device \"" + ChosenDevice.Name + "\" is NOT reachable. Is it still nearby?", AppLog.LogCategory.Warning);
                }
            }
        }

        #endregion

        #region TCP Connection Functions 

        private async void ConnectToServer(HostName hostname, string port) {
            if (!ServerConnected) {
                //AddLog($"Creating connection [host: {hostname.RawName}, port: {port}]", AppLog.LogCategory.Debug);

                this.ServerStatusSquare.Fill = new SolidColorBrush(Windows.UI.Colors.Yellow);
                this.ServerStatusText.Text = $"Connecting to server...";

                if (StreamSocket == null)
                    StreamSocket = new StreamSocket();

                try {
                    await StreamSocket.ConnectAsync(hostname, port.ToString());
                    ServerConnected = true;
                    this.ServerStatusSquare.Fill = new SolidColorBrush(Windows.UI.Colors.Green);
                    this.ServerStatusText.Text = $"Connected to server.";
                    //AddLog($"Connected [key: {StreamSocket.Information.SessionKey}]", AppLog.LogCategory.Debug);

                } catch (Exception e) {
                    //AddLog("Problem while connecting to server.\n" +
                    //    "Message: " + e.Message +
                    //    "StackTrace: \n" + e.StackTrace, AppLog.LogCategory.Debug);
                    ServerConnected = false;
                }
            }
        }

        private void DisconnectFromServer() {
            if (ServerConnected) {
                AddLog($"Disconnecting from server...", AppLog.LogCategory.Debug);
                try {
                    StreamSocket.Dispose();

                } catch (Exception e){
                    AddLog($"ERROR while disconnecting. \n\tMessage:{e.Message}\n\tStackTrace: {e.StackTrace}", AppLog.LogCategory.Debug);
                }
                ServerConnected = false;
                AddLog($"Server disconnected.", AppLog.LogCategory.Debug);
            }
        }

        // returns a default value if the port value set by the user is not valid
        string CheckServerPort() {
            int result;
            if (Int32.TryParse(appSettings.ServerTCPPort, out result)) {
                return appSettings.ServerTCPPort;
            } else {
                return appSettings.defaultPort;
            }
        }

        // timer setup (suggested tick rate is 2 seconds)
        private void DispatcherTimerSetup() {
            if (DispatcherTimer == null) {
                DispatcherTimer = new DispatcherTimer();            
            }

            DispatcherTimer.Tick -= DispatcherTimer_Tick;           // remove a function from being called
            DispatcherTimer.Tick += DispatcherTimer_Tick;           // set a function to be called
            DispatcherTimer.Interval = new TimeSpan(0, 0, 1);       // every second send to 
            DispatcherTimer.Start();                                // and start it
        }

        // every second send the message to tcp listeners
        private void DispatcherTimer_Tick(object sender, object e) {
            // for tcp connection test purposes only
            if (DeviceConnected)
                PingDevice();       // ping bluetooth device to verify if it's still nearby

            if (!ServerConnected) {
                ServerHost = new HostName(appSettings.ServerHostName.ToString());
                ConnectToServer(ServerHost, CheckServerPort());
            } else {
                SendToClients(BPMValue);
            }
        }

        // sends a message to clients connected to the server
        private async void SendToClients(string message) {
            try {
                Debug.WriteLine("Sending message: " + message);
                Stream streamOut = StreamSocket.OutputStream.AsStreamForWrite();
                StreamWriter writer = new StreamWriter(streamOut);
                await writer.WriteLineAsync(message);
                await writer.FlushAsync();

            } catch (Exception e) {
                Debug.WriteLine(e.StackTrace);
            }
        }

        #endregion

        bool testingServer = false;

        private async void ResetButton_Click(object sender, RoutedEventArgs e) {

            if (testingServer == false) {
                DispatcherTimerSetup();
                testingServer = true;
                ResetButton.Content = "End TCP Connection Test";

                AddLog($"Starting server connection protocol.", AppLog.LogCategory.Debug);
            } else {
                DispatcherTimer.Stop();
                testingServer = false;
                ResetButton.Content = "Start TCP Connection Test";

                AddLog($"Stopping server connection protocol.", AppLog.LogCategory.Debug);
            }
        }
    }
}
