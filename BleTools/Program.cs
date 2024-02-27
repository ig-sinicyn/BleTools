using BleTools.Infrastructure;
using BleTools.Infrastructure.Backported;

using Cocona;
using Cocona.Command;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BleTools
{
	internal class Program
	{
		public static async Task Main(string[] args)
		{
			var debugFlag = $"--{WellKnownGlobalOptions.DebugFlag.Name}";
			var debugMode = args.Any(x => x.Equals(debugFlag, StringComparison.OrdinalIgnoreCase));

			var builder = CoconaApp.CreateBuilder(args);
			builder.Configuration.AddInMemoryCollection(new KeyValuePair<string, string>[]
			{
				new("Logging:LogLevel:Microsoft.Extensions.Hosting", nameof(LogLevel.Information))
			});
			builder.Logging
				.ClearProviders()
				.SetMinimumLevel(debugMode ? LogLevel.Debug : LogLevel.Information)
				.AddConsole(options => options.FormatterName = PlainConsoleFormatterOptions.FormatterName)
				.AddConsoleFormatter<PlainConsoleFormatter, PlainConsoleFormatterOptions>();
			builder.Services.AddOptions<BluetoothOptions>();
			builder.Services.AddSingleton<BluetoothService>();
			builder.Services.Decorate<ICoconaCommandProvider, CustomGlobalOptionsCommandProvider>();

			try
			{
				using var app = builder.Build();
				app.AddCommands<DeviceCommands>();
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