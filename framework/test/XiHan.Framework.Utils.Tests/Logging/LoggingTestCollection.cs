// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Tests.Logging;

/// <summary>
/// 日志测试集合
/// </summary>
/// <remarks>
/// <see cref="XiHan.Framework.Utils.Logging.LogFileHelper"/> 是静态类，日志目录、缓冲区大小、
/// 文件大小上限等均为进程级全局配置；<see cref="XiHan.Framework.Utils.Logging.LogHelper"/>
/// 的对应设置最终也转调它。各测试类在构造函数里把日志目录指向各自的临时目录，
/// 若并行执行则互相覆盖该全局配置，测试写完日志再回自己目录查找时会一无所获。
/// 归入同一集合以保证类间串行。
/// </remarks>
[CollectionDefinition(Name)]
public class LoggingTestCollection
{
    /// <summary>
    /// 集合名称
    /// </summary>
    public const string Name = "Logging";
}
