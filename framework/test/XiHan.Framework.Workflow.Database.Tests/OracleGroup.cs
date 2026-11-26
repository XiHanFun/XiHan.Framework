// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 连真实 Oracle 的用例
/// </summary>
/// <remarks>
/// 每个用例新建用户并在空库上建全部工作流表，建用户与建表都是同步的阻塞调用、在 CI 上一次几十秒；几个这样的类同时跑会占住线程池，同一进程里别的用例（如 SQLite 的宿主启动）被拖过 30 秒的启动期限。这个集合不与其他集合并行，集合内的类依次运行
/// </remarks>
[CollectionDefinition(nameof(OracleGroup), DisableParallelization = true)]
public sealed class OracleGroup;
