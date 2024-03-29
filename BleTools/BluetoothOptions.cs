﻿namespace BleTools;

public class BluetoothOptions
{
	public TimeSpan DeviceDiscoveryTimeout { get; set; } = TimeSpan.FromSeconds(20);

	public TimeSpan MetadataRetrieveTimeout { get; set; } = TimeSpan.FromSeconds(10);

	public TimeSpan MetadataPollingInterval { get; set; } = TimeSpan.FromMilliseconds(100);
}