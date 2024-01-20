using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Devices.WiFiDirect;
using Windows.Foundation;
using Windows.Storage.Streams;

using static System.Runtime.InteropServices.JavaScript.JSType;

using Buffer = Windows.Storage.Streams.Buffer;

namespace BleSend
{
	internal class Program
	{
		private static readonly Guid _serviceId = new Guid("0000ffef-0000-1000-8000-00805f9b34fb");
		private static readonly Guid _charId = new Guid("A1FF12BB-3ED8-46E5-B4F9-D64E2FEC021B");

		static async Task Main(string[] args)
		{
			var deviceId = "BluetoothLE#BluetoothLEf8:28:19:b5:b8:3a-dc:a6:32:60:c9:56";

			using var device = await BluetoothLEDevice.FromIdAsync(deviceId);

			if (device.DeviceInformation.Pairing.IsPaired == false)
			{
				var customPairing = device.DeviceInformation.Pairing.Custom;
				var ceremoniesSelected = DevicePairingKinds.ProvidePin | DevicePairingKinds.ConfirmOnly | DevicePairingKinds.ConfirmPinMatch | DevicePairingKinds.DisplayPin;
				var protectionLevel = DevicePairingProtectionLevel.EncryptionAndAuthentication;
				customPairing.PairingRequested += PairingRequested;
				var result = await customPairing.PairAsync(
					ceremoniesSelected,
					protectionLevel);

			}

			var s = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
			using var svc = s.Services.First(x => x.Uuid == _serviceId);
			svc.Session.MaintainConnection = true;
			var characteristics = (await svc.GetCharacteristicsForUuidAsync(_charId));

			var ch = characteristics.Characteristics.Single();
			ch.ProtectionLevel = GattProtectionLevel.EncryptionAndAuthenticationRequired;

			// Read
			var readResult = await ch.ReadValueAsync(BluetoothCacheMode.Uncached);
			if (readResult.Status == GattCommunicationStatus.Success)
			{
				var reader = DataReader.FromBuffer(readResult.Value);
				var input = new byte[reader.UnconsumedBufferLength];
				reader.ReadBytes(input);
				Console.WriteLine("Read: " + Encoding.UTF8.GetString(input));
			}
			else
			{
				Console.WriteLine("Fail");
			}

			// Write
			var writer = new DataWriter();
			writer.WriteBytes("alt-tab"u8.ToArray());
			var writeResult = await ch.WriteValueAsync(writer.DetachBuffer());
			if (writeResult == GattCommunicationStatus.Success)
			{
				Console.WriteLine("Write!");
				// Successfully wrote to device
			}
			else
			{
				Console.WriteLine("Fail!");
			}
		}

		private static void PairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
		{
			args.Accept(args.Pin);
		}

		private static void PairingRequestedHandler(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
		{
			switch (args.PairingKind)
			{
				case DevicePairingKinds.ConfirmOnly:
					// Windows itself will pop the confirmation dialog as part of "consent" if this is running on Desktop or Mobile
					// If this is an App for 'Windows IoT Core' or a Desktop and Console application
					// where there is no Windows Consent UX, you may want to provide your own confirmation.
					args.Accept();
					break;

				case DevicePairingKinds.ProvidePin:
					// A PIN may be shown on the target device and the user needs to enter the matching PIN on 
					// this Windows device. Get a deferral so we can perform the async request to the user.
					var collectPinDeferral = args.GetDeferral();
					var pinFromUser = "952693";
					if (!string.IsNullOrEmpty(pinFromUser))
					{
						args.Accept(pinFromUser);
					}
					collectPinDeferral.Complete();
					break;
			}
		}

		private static async Task PairAsync()
		{
			var id = @"BluetoothLE#BluetoothLEf8:28:19:b5:b8:3a-dc:a6:32:60:c9:56";

			var device = await BluetoothLEDevice.FromIdAsync(id);


		}

		private static async Task Test1()
		{

			// BT_Code: Example showing paired and non-paired in a single query.
			var aqsAllBluetoothLEDevices =
				"(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";

			var selector1 =
				@"System.Devices.DevObjectType:=5 AND System.Devices.Aep.ProtocolId:=""{BB7BB05E-5972-42B5-94FC-76EAA7084D49}"" AND (System.Devices.Aep.IsPaired:=System.StructuredQueryType.Boolean#True OR System.Devices.Aep.Bluetooth.IssueInquiry:=System.StructuredQueryType.Boolean#False)";
			var selector2 = @"(System.Devices.Aep.ProtocolId:=""{BB7BB05E-5972-42B5-94FC-76EAA7084D49}"")";
			var selector = BluetoothLEDevice.GetDeviceSelector();


			string[] requestedProperties =
			{
				"System.Devices.Aep.DeviceAddress",
				"System.Devices.Aep.IsConnected",
				"System.Devices.Aep.Bluetooth.Le.IsConnectable"
			};
			var all = await DeviceInformation.FindAllAsync(aqsAllBluetoothLEDevices, requestedProperties);
			var x = all.ToArray();

			var id = all.First(x => x.Name.Contains("NOT")).Id;

			var device = await BluetoothLEDevice.FromIdAsync(id);

			var svc = device.GattServices.First(x => x.Uuid == _serviceId);
			var characteristics = (await svc.GetCharacteristicsForUuidAsync(_charId));

			var ch = characteristics.Characteristics.Single();

			Console.WriteLine(await ch.ReadValueAsync());
		}

		private static async Task<DeviceInformation[]> FindAllAsync()
		{
			var collected = new ConcurrentBag<DeviceInformation>();
			var waitTask = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

			string[] requestedProperties =
			{
				"System.Devices.Aep.DeviceAddress",
				"System.Devices.Aep.IsConnected",
				"System.Devices.Aep.Bluetooth.Le.IsConnectable"
			};

			// BT_Code: Example showing paired and non-paired in a single query.
			var aqsAllBluetoothLEDevices =
				"(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";

			var deviceWatcher = DeviceInformation.CreateWatcher(
				aqsAllBluetoothLEDevices,
				requestedProperties,
				DeviceInformationKind.AssociationEndpoint
			);
			try
			{

				// Register event handlers before starting the watcher.
				deviceWatcher.Added += (sender, info) =>
				{
					collected.Add(info);
					if (info.Name.Contains("NOT"))
						waitTask.SetResult();
				};

				deviceWatcher.Start();

				await waitTask.Task;
			}
			finally
			{

				deviceWatcher.Stop();
			}

			return collected.DistinctBy(x => x.Id).ToArray();
		}

		private static async Task ListAllAsync()
		{
			var observed = new ConcurrentDictionary<string, string>();

			DeviceWatcher deviceWatcher;
			string[] requestedProperties =
			{
				"System.Devices.DevObjectType",
				"System.Devices.Aep.DeviceAddress",
				"System.Devices.Aep.IsConnected",
				"System.Devices.Aep.IsPaired",
				"System.Devices.Aep.Bluetooth.Le.IsConnectable",
				"System.Devices.Aep.Bluetooth.IssueInquiry"
			};

			// BT_Code: Example showing paired and non-paired in a single query.
			var simpleSelector =
				"(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";

			// System.Devices.DevObjectType:=5 AND System.Devices.Aep.ProtocolId:="{BB7BB05E-5972-42B5-94FC-76EAA7084D49}" AND (System.Devices.Aep.IsPaired:=System.StructuredQueryType.Boolean#True OR System.Devices.Aep.Bluetooth.IssueInquiry:=System.StructuredQueryType.Boolean#False)
			var selector = @"System.Devices.DevObjectType:=5 
				AND System.Devices.Aep.ProtocolId:=""{BB7BB05E-5972-42B5-94FC-76EAA7084D49}"" 
				AND (System.Devices.Aep.IsPaired:=System.StructuredQueryType.Boolean#True
					OR System.Devices.Aep.Bluetooth.IssueInquiry:=System.StructuredQueryType.Boolean#False)";

			deviceWatcher = DeviceInformation.CreateWatcher(
				simpleSelector,
				requestedProperties,
				DeviceInformationKind.AssociationEndpoint
			);

			// Register event handlers before starting the watcher.
			deviceWatcher.Added += (sender, info) =>
			{
				if (observed.ContainsKey(info.Id))
				{
					Console.WriteLine($"(already seen) {info.Name ?? "Unknown"} ({info.Id})");
					return;
				}

				Console.WriteLine($"Found device: {info.Name ?? "Unknown"} ({info.Id})");

				if (info.Name?.Contains("OnePlus") ?? false)
				{

				}
				if (info.Name?.Contains("Soundcore") ?? false)
				{

				}
				if (info.Name?.Contains("NOTIFY") ?? false)
				{
					var resp = info.Pairing.Custom.PairAsync(DevicePairingKinds.ConfirmPinMatch, DevicePairingProtectionLevel.EncryptionAndAuthentication).GetResults();
					Console.WriteLine(resp.ProtectionLevelUsed);
				}

				observed.AddOrUpdate(info.Id, info.Id, (x, _) => x);
			};

			deviceWatcher.Updated += (sender, info) =>
			{
				// updated must be not null or search won't be performed
			};

			deviceWatcher.Start();
			try
			{

				Console.WriteLine("Begin scanning");

				await Task.Delay(TimeSpan.FromDays(1));
			}
			finally
			{

				deviceWatcher.Stop();
			}
		}

	}
}
