using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace Parakeet_3._0.Models {

    public class BTDevice {
        public string DeviceName { set; get; }
        public string DeviceId { set; get; }
        public DeviceInformation DeviceInfo { set; get; }

        public BTDevice() {
            this.DeviceName = "None";
            this.DeviceId = "None";
            DeviceInfo = null;
        }

        public BTDevice(string name, string id) {
            this.DeviceName = name;
            this.DeviceId = id;
            DeviceInfo = null;
        }

        public BTDevice(DeviceInformation devInfo) {
            this.DeviceName = devInfo.Name;
            this.DeviceId = devInfo.Id;
            this.DeviceInfo = devInfo;
        }
    }
}
