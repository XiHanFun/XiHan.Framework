// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine;

/// <summary>
/// 命令行的入口：解析参数、找命令、在新作用域里执行并返回退出码。没有命令或 <c>help</c> 时打印帮助；
/// 命令名不存在但 <c>&lt;resource&gt; &lt;verb&gt;</c> 能拼成操作 ID 时转成 <c>invoke &lt;resource&gt;.&lt;verb&gt;</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class CommandLineRunner
{
    /// <summary>
    /// <c>invoke</c> 命令名。
    /// </summary>
    private const string InvokeCommandName = "invoke";

    /// <summary>
    /// <c>help</c> 命令名。
    /// </summary>
    private const string HelpCommandName = "help";

    /// <summary>
    /// 根服务；每次执行从它创建作用域。
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 命令名 → 命令。
    /// </summary>
    private readonly SortedDictionary<string, ICommand> _commands = new(StringComparer.Ordinal);

    /// <summary>
    /// 创建；命令名重复即失败。
    /// </summary>
    public CommandLineRunner(IServiceProvider services, IEnumerable<ICommand> commands)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        ArgumentNullException.ThrowIfNull(commands);
        foreach (var command in commands)
        {
            if (!_commands.TryAdd(command.Name, command))
            {
                throw new ArgumentException($"Command name '{command.Name}' is registered more than once.", nameof(commands));
            }
        }
    }

    /// <summary>
    /// 已注册的命令，按名字排序。
    /// </summary>
    public IReadOnlyCollection<ICommand> Commands => _commands.Values;

    /// <summary>
    /// 执行一条命令行。
    /// </summary>
    public async ValueTask<int> RunAsync(IReadOnlyList<string> args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        var parsed = CommandLineParser.Parse(args);

        // 没有命令是用法错误（退出码 64），显式 help 是成功
        if (parsed.Command is null || parsed.HasOption(HelpCommandName))
        {
            await CommandLineHelp.WriteAsync(parsed.Command is null ? error : output, _commands.Values).ConfigureAwait(false);
            return parsed.Command is null ? CommandLineExitCodes.Usage : CommandLineExitCodes.Success;
        }

        await using var scope = _services.CreateAsyncScope();
        if (!_commands.TryGetValue(parsed.Command, out var command))
        {
            // 简写：orders create … → invoke orders.create …
            if (parsed.Arguments.Count > 0 && OperationId.IsValid(parsed.Command + "." + parsed.Arguments[0]) && _commands.TryGetValue(InvokeCommandName, out var invoke))
            {
                var shorthand = parsed with { Command = InvokeCommandName, Arguments = [parsed.Command + "." + parsed.Arguments[0], .. parsed.Arguments.Skip(1)] };
                return await ExecuteAsync(invoke, new CommandContext(shorthand, scope.ServiceProvider, output, error, cancellationToken)).ConfigureAwait(false);
            }

            await CommandLineFailure.WriteAsync(error, Error.NotFound(CommandLineErrors.CommandUnknown, CommandLineMessages.CommandUnknown, MessageArguments.Of(("command", parsed.Command))), scope.ServiceProvider, null, CommandLineExitCodes.Usage).ConfigureAwait(false);
            return CommandLineExitCodes.Usage;
        }

        return await ExecuteAsync(command, new CommandContext(parsed, scope.ServiceProvider, output, error, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// 执行命令；取消按取消退出码返回，不当作崩溃。
    /// </summary>
    private static async ValueTask<int> ExecuteAsync(ICommand command, CommandContext context)
    {
        try
        {
            return await command.ExecuteAsync(context).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            return CommandLineExitCodes.Cancelled;
        }
    }
}
