// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Messaging.RabbitMQ.Tests;

/// <summary>
/// 打桩通道记下的一次队列声明
/// </summary>
/// <param name="Name">队列名</param>
/// <param name="Durable">是否持久</param>
/// <param name="Exclusive">是否独占（随连接删除）</param>
/// <param name="AutoDelete">最后一个消费者离开后是否自动删除</param>
public sealed record QueueDeclaration(string Name, bool Durable, bool Exclusive, bool AutoDelete);
