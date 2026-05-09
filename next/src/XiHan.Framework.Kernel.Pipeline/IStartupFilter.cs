// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// 管道启动过滤器。特性可实现此接口来声明中间件的插入位置。
/// </summary>
[ApiLevel(Stability.Stable, "1.0")]
public interface IStartupFilter
{
    /// <summary>
    /// 配置管道中间件的插入位置。
    /// </summary>
    /// <param name="next">后续配置委托。</param>
    /// <returns>包装后的管道配置委托。</returns>
    Action<PipelineBuilder> Configure(Action<PipelineBuilder> next);
}
