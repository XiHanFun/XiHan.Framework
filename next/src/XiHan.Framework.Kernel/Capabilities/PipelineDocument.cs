// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Capabilities;

internal sealed class PipelineDocument
{
    public string? Middleware { get; set; }

    public string? Stage { get; set; }

    public List<string>? Before { get; set; }

    public List<string>? After { get; set; }
}
