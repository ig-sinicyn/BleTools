using BleSend.Temp;
using Cocona;

using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.IO;

using System.Runtime.Intrinsics.X86;
namespace BleSend
{
	internal class Program
	{
		public static async Task Main(string[] args)
		{
			var deviceId = await SimpleTest.DiscoverDeviceIdAsync();
			await SimpleTest.PairAsync(deviceId);
			await SimpleTest.TestServiceAsync(deviceId);

			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			return;

			var builder = CoconaApp.CreateBuilder(args);
			builder.Logging.AddDebug();
			using var app = builder.Build();
			app.AddCommands<PairCommands>();
			app.AddCommands<CharacteristicCommands>();
			await app.RunAsync();

			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
		}
	}
}
