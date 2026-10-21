// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace SamplePlugin;

/// <summary>
/// 插件私有服务。
/// </summary>
public sealed class Greeter
{
    private int _greetings;

    public int Greetings => _greetings;

    public string Greet(string name)
    {
        _greetings++;
        return $"Hello from plugin, {name}!";
    }
}
