// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine;

/// <summary>
/// <c>operations</c>：按行列出登记表里的操作：ID、稳定性、暴露的协议、权限或策略，制表符分隔。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class OperationsCommand : ICommand
{
    /// <inheritdoc />
    public string Name => "operations";

    /// <inheritdoc />
    public string DescriptionKey => CommandLineMessages.OperationsDescription;

    /// <inheritdoc />
    public async ValueTask<int> ExecuteAsync(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (var descriptor in context.Services.GetRequiredService<OperationRegistry>().All())
        {
            var contract = descriptor.Contract;
            var authorization = contract.Permission is { } permission ? "permission=" + permission : contract.Policy is { } policy ? "policy=" + policy : "-";
            await context.Output.WriteLineAsync($"{descriptor.Id}\t{contract.Stability}\t{contract.Expose}\t{authorization}").ConfigureAwait(false);
        }

        return CommandLineExitCodes.Success;
    }
}
