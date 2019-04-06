# Parakeet_3x
BLE GATT client made for Windows (UWP) that read the heart rate from BLE devices and send the values via TCP/IP protocol

<b>The app has been tested with:</b>
- Pebble 2 HR
- Amazfit Bip
- Xiaomi Mi Band 2

<b>The app won't work with:</b>
- Most fitbit devices
- Apple watch
- Wear OS devices (Android)

<b>The app is likely to work with:</b>
- Polar heart rate chest band,
- Most Bluetooth Low Energy devices with an integrated GATT server

⚠ Visual studio will take care of most problems by downloading a ton of libraries, but you have to manually activate the bluetooth component from the project settings ⚠

<b>How to use Parakeet:</b>

1. Pair your Bluetooth device to your pc using the windows settings
2. Launch parakeet
3. If your device has a continuos heart rate function or a "fitness" mode, activate it
4. Select your device from the in-app device list
5. You can choose a TCP port and address to send the heart rate values
6. Click connect and wait for the values

<b>Things you should know:</b>
- This is a spike solution for a problem encountered during the development of a bigger university project
- It is still unstable when changing device. Fast solution: restart the app any time you want to change the device to connect
