﻿using System;
using System.Linq;

using JetBrains.Annotations;

using Rsdn.SmartApp;

namespace Rsdn.Janus
{
	[Service(typeof(ICommandHandlerService))]
	public sealed class CommandHandlerService : ICommandHandlerService
	{
		private readonly ICommandService _commandService;
		private readonly ICommandTarget[] _commandTargets;

		public CommandHandlerService([NotNull] IServiceProvider serviceProvider)
		{
			if (serviceProvider == null)
				throw new ArgumentNullException("serviceProvider");

			_commandService = serviceProvider.GetRequiredService<ICommandService>();
			_commandTargets = 
				new ExtensionsCache<CommandTargetInfo, ICommandTarget>(serviceProvider).GetAllExtensions();
		}

		#region Implementation of ICommandHandlerService

		public void ExecuteCommand(
			[NotNull] string commandName,
			[NotNull] ICommandContext context)
		{
			if (commandName == null)
				throw new ArgumentNullException("commandName");
			if (context == null)
				throw new ArgumentNullException("context");

			CheckCommandAndParameters(commandName, context);

			var isAvaliable = false;
			foreach (var target in _commandTargets)
				if (target.QueryStatus(commandName, context) == CommandStatus.Normal)
				{
					target.Execute(commandName, context);
					isAvaliable = true;
				}

			if (!isAvaliable)
				throw new InvalidOperationException(
					"Команда не может быть выполнена, так как этого не позволяет её статус.");
		}

		public CommandStatus QueryStatus(
			[NotNull] string commandName,
			[NotNull] ICommandContext context)
		{
			if (commandName == null)
				throw new ArgumentNullException("commandName");
			if (context == null)
				throw new ArgumentNullException("context");

			CheckCommandAndParameters(commandName, context);

			var disabled = false;

			foreach (var target in _commandTargets)
			{
				var status = target.QueryStatus(commandName, context);
				if (status == CommandStatus.Normal)
					return CommandStatus.Normal;
				if (status == CommandStatus.Disabled)
					disabled = true;
			}

			return disabled ? CommandStatus.Disabled : CommandStatus.Unavailable;
		}

		public IDisposable SubscribeCommandStatusChanged(
			IServiceProvider serviceProvider,
			EventHandler<ICommandHandlerService, string[]> handler)
		{
			if (serviceProvider == null)
				throw new ArgumentNullException("serviceProvider");
			if (handler == null)
				throw new ArgumentNullException("handler");

			return
				_commandTargets
					.Select(
						target =>
							target.SubscribeStatusChanged(
								serviceProvider,
								(sender, commansIds) => handler(this, commansIds)))
					.Merge();
		}

		#endregion

		#region Private Members

		private void CheckCommandAndParameters(string commandName, ICommandContext context)
		{
			var commandInfo = _commandService.GetCommandInfo(commandName);
			foreach (var parameter in commandInfo.Parameters)
				if (!parameter.IsOptional && !context.IsParameterExists(parameter.Name))
					throw new ApplicationException(
						"Обязательный параметр '{0}' команды '{1}' отсутствует в контексте."
							.FormatStr(parameter.Name, commandName));
		}

		#endregion
	}
}