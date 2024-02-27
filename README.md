# BleTools

This repository contains two Windows-only utilities for managing GATT service characteristics of Bluetooth LE devices.

# BleTools.Write
The utility is a single-purpose lightweight tool that allows only writing value into specified GATT service characteristic. Value is written as UTF-8 string; other formats are not supported yet.

The utility is published as a single-file app and is heavily tuned to reduce startup and connection times. The downside is, the writes may fail under some conditions (the target device is sleeping, OS-level GATT cache is out of date and so on). The recommended approach is to use full-functional `BleTools write` command and then switch to the `BleTools.Write` to reduce command execution time.

**Usage:**
```
Usage: BleTools.Write bluetooth-address service characteristic value

Writes GATT service characteristic

Arguments:
  0: bluetooth-address    MAC address of the Bluetooth LE device (Required)
  1: service              GATT service UUID (Required)
  2: characteristic       GATT service characteristic UUID (Required)
  3: value                The new characteristic value (passed as UTF-8 string) (Required)

Options:
  --debug        Enables debug output
  -h, --help     Show help message
```

**Example:**
```
> .\BleTools.Write.exe B8:27:EB:9C:F6:4C 00000000-6907-4437-8539-9218a9d54e29 00000001-6907-4437-8539-9218a9d54e29 "Test Value"

Value 'Test Value' written to service / characteristic 00000000-6907-4437-8539-9218a9d54e29 / 00000001-6907-4437-8539-9218a9d54e29 (device B8:27:EB:9C:F6:4C).
```

# BleTools
The utility provides a set of commands for working with Bluetooth LE devices and its  GATT service characteristics.

## `scan` command
The commands begins scanning for available Bluetooth devices and reports them until scan is stopped.

**Usage:**
```
Usage: BleTools scan [--filter <BluetoothDeviceFilter>] [--help]

Scans for available Bluetooth devices

Options:
  -f, --filter <BluetoothDeviceFilter>    Device filter (Default: BluetoothLe) (Allowed values: BluetoothLe, BluetoothClassic, All)
  --debug                                 Enables debug output
  -h, --help                              Show help message
```

**Example:**
```
> .\BleTools.exe scan
info:  Begin scanning for Bluetooth devices (filter set to BluetoothLe). Press Ctrl-C to stop the scanning.
info:  * Bluetooth LE device 49:6D:A9:71:C0:DD discovered:
info:     - RSSI: -82, Status: Unpaired;
info:  * Bluetooth LE device B8:27:EB:9C:F6:4C (Raspberry PI) discovered:
info:     - RSSI: -76, Status: Paired;
info:  * Bluetooth LE device 51:C7:1C:23:E2:5F discovered:
info:     - RSSI: -88, Status: Unpaired;
info:  * Bluetooth LE device 49:6D:A9:71:C0:DD discovered (already seen);
info:  * Bluetooth LE device BF:A3:20:C7:25:A9 discovered:
info:     - RSSI: -92, Status: Unpaired;
info:  End scanning for Bluetooth devices.
PS D:\Projects\BleTools\BleTools\bin\Debug\net8.0-windows10.0.22621.0>
```

## `pair` command
The commands starts pairing for specified Bluetooth device (usually requires confirmation on the device side). For pairing to Linux-based console-only devices be sure to start `bluetoothctl` tool before pairing the device. If the windows device cannot discover Linux one, try to set `bluetoothctl discoverable on`.

**Usage:**
```
Usage: BleTools pair [--force] [--help] bluetooth-address

Starts pairing for specified device (usually requires confirmation on the target device)

Arguments:
  0: bluetooth-address    MAC address of the Bluetooth device (Required)

Options:
  -f, --force    Force pairing (unpair if already paired)
  --debug        Enables debug output
  -h, --help     Show help message
```

**Example:**
```
PS D:\Projects\BleTools\BleTools\bin\Debug\net8.0-windows10.0.22621.0> .\BleTools.exe pair B8:27:EB:9C:F6:4C
info:  Please confirm pairing on Raspberry Pi. Pairing accepted on this device (ConfirmPinMatch).
info:  Device B8:27:EB:9C:F6:4C (Raspberry Pi) pairing complete. Protection level: None.
```

Confirmation and trust example (Raspberry Pi console):
```
~ $ bluetoothctl
Agent registered
[CHG] Controller B8:27:EB:9C:F6:4C Pairable: yes
[bluetooth]# discoverable on
Changing discoverable on succeeded
[CHG] Controller B8:27:EB:9C:F6:4C Discoverable: yes
[NEW] Device F8:28:19:B5:B8:3A TestPC
Request confirmation
[agent] Confirm passkey 528302 (yes/no): yes
[CHG] Device F8:28:19:B5:B8:3A Bonded: yes
[CHG] Device F8:28:19:B5:B8:3A Paired: yes
[bluetooth]# trust F8:28:19:B5:B8:3A
[CHG] Device F8:28:19:B5:B8:3A Trusted: yes
[bluetooth]# exit
~ $
```

## `unpair` command
The commands removes pairing for specified Bluetooth device.

**Usage:**
```
Usage: BleTools unpair [--help] bluetooth-address

Revokes pairing for specified device

Arguments:
  0: bluetooth-address    MAC address of the Bluetooth device (Required)

Options:
  --debug       Enables debug output
  -h, --help    Show help message
```

 For completely unpairing Linux-based devices you may want to explicitly revoke trust /pair status. As example (Raspberry Pi console):
```
[bluetooth]# remove  F8:28:19:B5:B8:3A
[DEL] Device F8:28:19:B5:B8:3A TestPC
Device has been removed
```

**Example:**
```
> .\BleTools.exe unpair B8:27:EB:9C:F6:4C
info:  Device B8:27:EB:9C:F6:4C (Raspberry Pi) unpairing complete.
```

## `list` command
The commands lists GATT services and characteristics for specified Bluetooth device.

**Usage:**
```
Usage: BleTools list [--require-pairing] [--uncached] [--help] bluetooth-address

Lists GATT services and characteristics

Arguments:
  0: bluetooth-address    MAC address of the Bluetooth LE device (Required)

Options:
  -p, --require-pairing    Require the device to be paired
  -u, --uncached           Ignore OS-level GATT cache
  --debug                  Enables debug output
  -h, --help               Show help message
```

**Example:**
```
> .\BleTools.exe list B8:27:EB:9C:F6:4C
info:  4 service(s) found for B8:27:EB:9C:F6:4C (Raspberry Pi).
info:  * 00001800-0000-1000-8000-00805f9b34fb:
info:     - 00002a00-0000-1000-8000-00805f9b34fb: Read;
info:     - 00002a01-0000-1000-8000-00805f9b34fb: Read;
info:     - 00002aa6-0000-1000-8000-00805f9b34fb: Read;
info:  * 00001801-0000-1000-8000-00805f9b34fb:
info:     - 00002a05-0000-1000-8000-00805f9b34fb: Indicate;
info:     - 00002b29-0000-1000-8000-00805f9b34fb: Read, Write;
info:     - 00002b2a-0000-1000-8000-00805f9b34fb: Read;
info:     - 00002b3a-0000-1000-8000-00805f9b34fb: Read;
info:  * 0000180a-0000-1000-8000-00805f9b34fb:
info:     - 00002a50-0000-1000-8000-00805f9b34fb: Read;
info:  * 00000000-6907-4437-8539-9218a9d54e29:
info:     - 00000001-6907-4437-8539-9218a9d54e29: Read, Write;
```

## `read` command
The commands reads GATT service characteristic for specified Bluetooth device. The value is reported as UTF-8 string; other formats are not supported yet.

**Usage:**
```
Usage: BleTools read [--service <Guid>] [--characteristic <Guid>] [--require-pairing] [--uncached] [--help] bluetooth-address

Reads GATT service characteristic value as UTF-8 string

Arguments:
  0: bluetooth-address    MAC address of the Bluetooth LE device (Required)

Options:
  -s, --service <Guid>           GATT service UUID (Required)
  -c, --characteristic <Guid>    GATT service characteristic UUID (Required)
  -p, --require-pairing          Require the device to be paired
  -u, --uncached                 Ignore OS-level GATT cache
  --debug                        Enables debug output
  -h, --help                     Show help message
```

**Example:**
```
.\BleTools.exe read B8:27:EB:9C:F6:4C -s 00000000-6907-4437-8539-9218a9d54e29 -c 00000001-6907-4437-8539-9218a9d54e29
info:  00000001-6907-4437-8539-9218a9d54e29 value is 'Test value'.
```


## `write` command
The commands writes GATT service characteristic for specified Bluetooth device. The value is passed as UTF-8 string; other formats are not supported yet.

**Usage:**
```
Usage: BleTools write [--service <Guid>] [--characteristic <Guid>] [--require-pairing] [--uncached] [--help] bluetooth-address value

Writes GATT service characteristic

Arguments:
  0: bluetooth-address    MAC address of the Bluetooth LE device (Required)
  1: value                The new characteristic value (passed as UTF-8 string) (Required)

Options:
  -s, --service <Guid>           GATT service UUID (Required)
  -c, --characteristic <Guid>    GATT service characteristic UUID (Required)
  -p, --require-pairing          Require the device to be paired
  -u, --uncached                 Ignore OS-level GATT cache
  --debug                        Enables debug output
  -h, --help                     Show help message
```

**Example:**
```
> .\BleTools.exe write B8:27:EB:9C:F6:4C -s 00000000-6907-4437-8539-9218a9d54e29 -c 00000001-6907-4437-8539-9218a9d54e29 "Test value 2"
info:  00000001-6907-4437-8539-9218a9d54e29 set to 'Test value 2'.
```







































