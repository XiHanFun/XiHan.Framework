// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 本实例在消息代理上的标识。
/// </summary>
/// <remarks>
/// 由 <see cref="MessagingRegistration.Configure"/> 登记为单例，每个宿主一个。代理用它给广播订阅起本实例独占的名字（RabbitMQ 的队列、Kafka 的消费组，RFC-0009「订阅方式」，ADR-0079）。不是选项：不从配置读取，两个实例用了同一个标识，Kafka 的广播会退化成同一消费组里的竞争；每次构造重新生成保证唯一。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class MessagingInstance
{
    /// <summary>
    /// 机器名部分的最大长度。
    /// </summary>
    private const int MaxMachineLength = 32;

    /// <summary>
    /// 生成一个新的实例标识。
    /// </summary>
    public MessagingInstance()
    {
        // 机器名让运维在代理的管理界面里认出是哪台机器，进程 ID 区分同一台机器上的进程，
        // 随机后缀区分同一进程里的多个宿主（测试）与重启
        var machine = new StringBuilder(MaxMachineLength);
        foreach (var character in Environment.MachineName.ToLowerInvariant())
        {
            if (machine.Length == MaxMachineLength)
            {
                break;
            }

            machine.Append(character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' ? character : '-');
        }

        var host = machine.Length == 0 ? "host" : machine.ToString();
        Id = string.Create(CultureInfo.InvariantCulture, $"{host}-{Environment.ProcessId}-{Guid.NewGuid().ToString("N")[..8]}");
    }

    /// <summary>
    /// 实例标识。
    /// </summary>
    /// <remarks>
    /// 机器名（小写，<c>[a-z0-9-]</c> 以外的字符换成 <c>-</c>，最多 32 个字符）、进程 ID 与 8 位随机十六进制，以 <c>-</c> 连接。
    /// </remarks>
    public string Id { get; }
}
