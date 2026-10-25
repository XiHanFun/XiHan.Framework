// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace KubernetesApp.Endpoints;

/// <summary>
/// 领导选举状态。
/// </summary>
/// <param name="Holder">本实例的持有者标识。</param>
/// <param name="Election">选举实现的类型名。</param>
/// <param name="IsLeader">本实例是否持有租约。</param>
/// <param name="Ticks">本实例运行心跳作业的次数（只在领导上增长）。</param>
public sealed record LeaderView(string Holder, string Election, bool IsLeader, long Ticks);
