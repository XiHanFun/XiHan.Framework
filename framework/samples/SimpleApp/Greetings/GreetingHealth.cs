// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace SimpleApp.Greetings;

/// <summary>
/// 清单声明的健康项 <c>samples.greeting.counter</c> 的实现
/// </summary>
/// <remarks>
/// 计数服务在进程内、总是可用，所以恒为 Ready；说明里带当前计数，<c>/health</c> 上能看到。声明了健康项的包必须登记对应的 <see cref="IHealthContributor"/>（RFC-0002）
/// </remarks>
internal sealed class GreetingHealth(GreetingCounter counter) : IHealthContributor
{
    /// <inheritdoc />
    public IReadOnlyList<ContributorStatus> Check()
        => [new ContributorStatus(GreetingFeature.HealthName, LifecycleState.Ready)
        {
            Description = $"Greeting counter ({counter.Count} greetings so far)",
        }];
}
