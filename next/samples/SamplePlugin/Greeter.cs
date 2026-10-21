// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace SamplePlugin;

/// <summary>
/// 插件私有服务。
/// </summary>
public sealed class Greeter
{
    /// <summary>
    /// 已问候的次数。
    /// </summary>
    private int _greetings;

    /// <summary>
    /// 已问候的次数。
    /// </summary>
    public int Greetings => _greetings;

    /// <summary>
    /// 问候指定对象并计数。
    /// </summary>
    public string Greet(string name)
    {
        _greetings++;
        return $"Hello from plugin, {name}!";
    }
}
