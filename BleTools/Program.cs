using BleTools.Infrastructure.Backported;

using Cocona;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BleTools
{
	internal class Program
	{
		public static async Task Main(string[] args)
		{
			var builder = CoconaApp.CreateBuilder(args);
			builder.Configuration.AddInMemoryCollection(new KeyValuePair<string, string>[]
			{
				new("Logging:LogLevel:Microsoft.Extensions.Hosting", nameof(LogLevel.Information))
			});
			builder.Logging
				.ClearProviders()
				.SetMinimumLevel(LogLevel.Debug)
				.AddConsole(options => options.FormatterName = PlainConsoleFormatterOptions.FormatterName)
				.AddConsoleFormatter<PlainConsoleFormatter, PlainConsoleFormatterOptions>();
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