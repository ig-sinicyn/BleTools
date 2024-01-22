using Cocona;

using Microsoft.Extensions.Logging;

namespace BleSend
{
	internal class Program
	{
		static async Task Main(string[] args)
		{
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
