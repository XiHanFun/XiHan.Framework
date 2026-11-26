// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.CommandLine.Commands;
using XiHan.Framework.Hosting.CommandLine.Parsing;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine.Runtime;

/// <summary>
/// 命令行的入口
/// </summary>
/// <remarks>
/// 解析参数、找命令、在新作用域里执行并返回退出码。没有命令或 <c>help</c> 时打印帮助，带 <c>--help</c> 的命令行指向一个弃用的操作时在帮助之后标出来；命令名不存在但 <c>&lt;resource&gt; &lt;verb&gt;</c> 能拼成操作 ID 时转成 <c>invoke &lt;resource&gt;.&lt;verb&gt;</c>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class CommandLineRunner
{
    /// <summary>
    /// <c>invoke</c> 命令名
    /// </summary>
    private const string InvokeCommandName = "invoke";

    /// <summary>
    /// <c>help</c> 命令名
    /// </summary>
    private const string HelpCommandName = "help";

    /// <summary>
    /// 根服务
    /// </summary>
    /// <remarks>
    /// 每次执行从它创建作用域
    /// </remarks>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 命令名 → 命令
    /// </summary>
    private readonly SortedDictionary<string, ICommand> _commands = new(StringComparer.Ordinal);

    /// <summary>
    /// 创建
    /// </summary>
    /// <remarks>
    /// 命令名重复即失败
    /// </remarks>
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
    /// 已注册的命令，按名字排序
    /// </summary>
    public IReadOnlyCollection<ICommand> Commands => _commands.Values;

    /// <summary>
    /// 执行一条命令行
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
            var help = parsed.Command is null ? error : output;
            await CommandLineHelp.WriteAsync(help, _commands.Values).ConfigureAwait(false);
            // 帮助不逐个列出操作：问的那条命令指向一个弃用的操作时，在帮助之后标出来，文案与调用时 stderr 上的那一句相同（ADR-0280）
            if (FindDeprecatedOperation(parsed).TryGetValue(out var deprecated))
            {
                await help.WriteLineAsync().ConfigureAwait(false);
                await help.WriteLineAsync(CommandLineMessages.OperationDeprecated.L(("id", deprecated))).ConfigureAwait(false);
            }

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

            // 未知命令没有发起调用：文案按命令行给出的 --locale 翻译（ADR-0325）
            var unknown = new CommandContext(parsed, scope.ServiceProvider, output, error, cancellationToken);
            return await CommandLineFailure.WriteAsync(unknown, Error.NotFound(CommandLineErrors.CommandUnknown, CommandLineMessages.CommandUnknown, MessageArguments.Of(("command", parsed.Command))), CommandLineExitCodes.Usage).ConfigureAwait(false);
        }

        return await ExecuteAsync(command, new CommandContext(parsed, scope.ServiceProvider, output, error, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// 命令行指向的操作是暴露给命令行的弃用操作时，交出它的 ID
    /// </summary>
    /// <remarks>
    /// 认 <c>invoke &lt;operation-id&gt;</c> 与 <c>&lt;resource&gt; &lt;verb&gt;</c> 简写，判定与 <c>invoke</c> 相同：未登记、没有暴露给命令行或内部的操作都不算。容器里没有登记表时没有值，帮助照常打印
    /// </remarks>
    private Maybe<string> FindDeprecatedOperation(ParsedCommandLine parsed)
    {
        if (parsed.Command is null || parsed.Arguments.Count == 0)
        {
            return default;
        }

        string id;
        if (string.Equals(parsed.Command, InvokeCommandName, StringComparison.Ordinal))
        {
            id = parsed.Arguments[0];
        }
        else if (!_commands.ContainsKey(parsed.Command) && OperationId.IsValid(parsed.Command + "." + parsed.Arguments[0]))
        {
            // 与执行时同一条简写规则：已注册的命令名优先，不当作资源段
            id = parsed.Command + "." + parsed.Arguments[0];
        }
        else
        {
            return default;
        }

        return _services.GetService<OperationRegistry>() is { } registry
            && registry.Find(id).TryGetValue(out var descriptor)
            && descriptor.Contract.IsExposedTo(Expose.CommandLine)
            && descriptor.Contract.Stability == Stability.Deprecated
                ? Maybe.Some(id)
                : default;
    }

    /// <summary>
    /// 执行命令
    /// </summary>
    /// <remarks>
    /// 取消按取消退出码返回，不当作崩溃
    /// </remarks>
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
