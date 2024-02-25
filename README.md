# BleTools

This repository contains two utilities for managing the GATT service characteristics of Bluetooth LE devices.

# BleTools.Write
The utility is a single-purpose lightweight tool that allows only writing value into specified GATT service characteristic. Value is written as utf-8 string; other formats are not supported yet.

The utility is published as a single-file app and is heavily tuned to reduce startup and connection times. The downside is, the writes may fail under some conditions (the target device is sleeping, OS-level GATT cache is out of date and so on). The recommended approach is to use full-functional `BleTools` utility' write command and then switch to the `BleTools.Write` to reduce command execution time

## Usage:
**Syntax:**
```
BleTools.Write {bluetooth-address} {service} {characteristic} {value}
* bluetooth-address: MAC address of the bluetooth LE device
* service: UUID of the target GATT service
* characteristic: UUID of the target GATT service characteristic
* value: characteristic value to write (passed as UTF-8 string)
```

**Example:**
```
> BleTools.Write B8:27:EB:9C:F6:4C 00000000-6907-4437-8539-9218a9d54e29 00000001-6907-4437-8539-9218a9d54e29 "Test Value"

Value 'Test Value' written to service / characteristic 00000000-6907-4437-8539-9218a9d54e29 / 00000001-6907-4437-8539-9218a9d54e29 (device B8:27:EB:9C:F6:4C).
```

# BleTools