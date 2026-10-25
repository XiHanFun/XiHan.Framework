// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.AI.Conformance;

namespace XiHan.Framework.AI.OpenAI.Tests;

/// <summary>
/// OpenAI Provider 对着仿真端点通过 Provider 目录套件。
/// </summary>
public sealed class OpenAIProviderConformanceTests : AIProviderConformance
{
    /// <inheritdoc />
    protected override async Task<AIProviderHost> StartAsync()
    {
        var host = await OpenAITestHost.StartAsync(new FakeOpenAIHandler());
        return new AIProviderHost(host.Services, OpenAITestHost.ProviderName, OpenAITestHost.Dimensions, host);
    }
}
