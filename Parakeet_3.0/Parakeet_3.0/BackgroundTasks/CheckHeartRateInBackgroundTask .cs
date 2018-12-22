using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Core;
using Windows.Devices.Enumeration;

namespace Parakeet_3._0 {

    public sealed class CheckHeartRateInBackgroundTask : IBackgroundTask {

        private BackgroundTaskDeferral _defferal;
        private static MiBand2SDK.MiBand2 band = new MiBand2SDK.MiBand2();

        public void Run(IBackgroundTaskInstance taskInstance) {
            _defferal = taskInstance.GetDeferral();
            Debug.WriteLine("Process in background started.");
            StartReadProc();
            

            //_defferal.Complete();
        }

        async void StartReadProc() {
            if (await band.ConnectAsync() && band.Identity.IsAuthenticated()) {
                Debug.WriteLine($"{await band.Device.GetDeviceName()} connected -----------------------");

                // You will receive heart rate measurements from the band
                await band.HeartRate.SubscribeToHeartRateNotificationsAsync(async (sender, args) => {
                    int currentHeartRate = args.CharacteristicValue.ToArray()[1];
                    Debug.WriteLine($"Current heartrate from background task is {currentHeartRate} bpm ");

                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        Windows.UI.Core.CoreDispatcherPriority.High, () => {
                            Home.GetCurrent().AddLog($"BPM: {currentHeartRate}", Models.AppLog.LogCategory.Info);
                        });
                });
            }
        }

        public static async void RegisterAndRunAsync(DeviceInformation devInfo) {
            var taskName = typeof(CheckHeartRateInBackgroundTask).ToString();

            IBackgroundTaskRegistration checkHeartRateInBackground = BackgroundTaskRegistration.AllTasks.Values.FirstOrDefault(t => t.Name == taskName);

            if (band.IsConnected() && band.Identity.IsAuthenticated()) {

                if (checkHeartRateInBackground != null) {
                    // if the task is already executing, unregister it
                    checkHeartRateInBackground.Unregister(true);
                }

                var devTrigger = new DeviceUseTrigger();
                var taskBuilder = new BackgroundTaskBuilder {
                    Name = taskName,
                    TaskEntryPoint = typeof(CheckHeartRateInBackgroundTask).ToString(),
                    IsNetworkRequested = false
                };

                taskBuilder.SetTrigger(devTrigger);

                BackgroundTaskRegistration task = taskBuilder.Register();
                task.Completed += Task_Completed;
                task.Progress += Task_Progress;
                await devTrigger.RequestAsync(devInfo.Id);
            }
        }

        private static void Task_Progress(BackgroundTaskRegistration sender, BackgroundTaskProgressEventArgs args) {
            Debug.WriteLine($"Task {sender.Name} progress: {args.Progress.ToString()}");
        }

        private static void Task_Completed(BackgroundTaskRegistration sender, BackgroundTaskCompletedEventArgs args) {
            Debug.WriteLine($"Task \"{sender.Name}\" finished ({args.ToString()})");
        }

        //public async void Run(IBackgroundTaskInstance taskInstance) {
        //    _defferal = taskInstance.GetDeferral();

        //    if (await band.ConnectAsync()) {
        //        // You will receive heart rate measurements from the band
        //        await band.HeartRate.SubscribeToHeartRateNotificationsAsync((sender, args) => {
        //            int currentHeartRate = args.CharacteristicValue.ToArray()[1];
        //            System.Diagnostics.Debug.WriteLine($"Current heartrate from background task is {currentHeartRate} bpm ");

        //            //await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
        //            //    Windows.UI.Core.CoreDispatcherPriority.High, () => {
        //            //        Home.GetCurrent().AddLog($"BPM: {currentHeartRate}", Models.AppLog.LogCategory.Info);
        //            //    });
        //        });
        //    }

        //    _defferal.Complete();
        //}

        //public static async void RegisterAndRunAsync() {
        //    var taskName = typeof(CheckHeartRateInBackgroundTask).Name;
        //    IBackgroundTaskRegistration checkHeartRateInBackground = BackgroundTaskRegistration.AllTasks.Values.FirstOrDefault(t => t.Name == taskName);

        //    if (band.IsConnected() && band.Identity.IsAuthenticated()) {
        //        if (checkHeartRateInBackground != null)
        //            checkHeartRateInBackground.Unregister(true);

        //        var deviceTrigger = new DeviceUseTrigger();
        //        var deviceInfo = await band.Identity.GetPairedBand();

        //        var taskBuilder = new BackgroundTaskBuilder {
        //            Name = taskName,
        //            TaskEntryPoint = typeof(CheckHeartRateInBackgroundTask).ToString(),
        //            IsNetworkRequested = false
        //        };

        //        taskBuilder.SetTrigger(deviceTrigger);
        //        try {
        //            BackgroundTaskRegistration task = taskBuilder.Register();
        //        } catch (Exception e) {
        //            Debug.WriteLine($"Creazione background andata male. \n Message {e.Message}\nStacktrace: {e.StackTrace}");
        //            return;
        //        }

        //        await deviceTrigger.RequestAsync(deviceInfo.Id);
        //    }
        //}



    }
}
