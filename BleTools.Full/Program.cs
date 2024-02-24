using Cocona;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BleTools.Full
{
	internal class Program
	{
		public static async Task Main(string[] args)
		{
			var builder = CoconaApp.CreateBuilder(args);
			builder.Logging.AddDebug();
			builder.Services.AddOptions<BluetoothOptions>();
			builder.Services.AddSingleton<BluetoothService>();

			try
			{
				using var app = builder.Build();
				app.AddCommands<PairCommands>();
				app.AddCommands<CharacteristicCommands>();
				await app.RunAsync();
			}
			finally
			{
				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();
			}
		}
	}
}