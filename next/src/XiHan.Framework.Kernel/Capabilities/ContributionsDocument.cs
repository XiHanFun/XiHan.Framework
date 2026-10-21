// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Capabilities;

internal sealed class ContributionsDocument
{
    public List<PipelineDocument>? Pipeline { get; set; }

    public List<string>? Operations { get; set; }

    public ConfigDocument? Config { get; set; }

    public List<string>? Permissions { get; set; }

    public List<string>? Health { get; set; }
}
