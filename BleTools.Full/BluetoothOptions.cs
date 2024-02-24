namespace BleSend;

public class BluetoothOptions
{
	public TimeSpan DiscoveryTimeout { get; set; } = TimeSpan.FromSeconds(10);

	public TimeSpan MetadataRetrieveTimeout { get; set; } = TimeSpan.FromSeconds(2);
}