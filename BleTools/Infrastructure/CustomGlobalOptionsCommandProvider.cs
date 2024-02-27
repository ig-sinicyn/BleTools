using Cocona.Command;

namespace BleTools.Infrastructure;

class CustomGlobalOptionsCommandProvider : ICoconaCommandProvider
{
	private readonly ICoconaCommandProvider _underlyingCommandProvider;
	private CommandCollection? _cachedCommandCollection;

	public CustomGlobalOptionsCommandProvider(ICoconaCommandProvider underlyingCommandProvider)
	{
		_underlyingCommandProvider = underlyingCommandProvider;
	}

	public CommandCollection GetCommandCollection()
	{
		return _cachedCommandCollection ??= GetWrappedCommandCollection(_underlyingCommandProvider.GetCommandCollection());
	}

	private static CommandCollection GetWrappedCommandCollection(CommandCollection commandCollection, int depth = 0)
	{
		var commands = commandCollection.All;

		// Inject debug option
		var newCommands = new CommandDescriptor[commands.Count];
		for (var i = 0; i < commands.Count; i++)
		{
			var command = commands[i];
			newCommands[i] = new CommandDescriptor(
				command.Method,
				command.Target,
				command.Name,
				command.Aliases,
				command.Description,
				command.Metadata,
				command.Parameters,
				BuildOptionDescriptor(command),
				command.Arguments,
				command.Overloads,
				command.OptionLikeCommands,
				command.Flags,
				command.SubCommands != null && command.SubCommands != commandCollection ? GetWrappedCommandCollection(command.SubCommands, depth + 1) : command.SubCommands
			);
		}

		static IReadOnlyList<CommandOptionDescriptor> BuildOptionDescriptor(CommandDescriptor command)
		{
			var options = command.Options.AsEnumerable();

			var allNames = new HashSet<string>(command.Options.Select(x => x.Name).Concat(command.OptionLikeCommands.Select(x => x.Name)));
			if (!allNames.Contains(WellKnownGlobalOptions.DebugFlag.Name))
			{
				options = options.Append(WellKnownGlobalOptions.DebugFlag);
			}

			return options.ToArray();
		}

		return new CommandCollection(newCommands);
	}
}