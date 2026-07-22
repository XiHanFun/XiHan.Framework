// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Activities.BuiltIn;

/// <summary>
/// HTTP 请求活动
/// </summary>
/// <remarks>
/// 节点属性：<c>Url</c>（请求地址，支持模板）；<c>Method</c>（请求方法，默认 GET）；
/// <c>Headers</c>（请求头字典，值支持模板）；<c>Body</c>（请求体，字符串支持模板，对象序列化为 JSON）；
/// <c>ContentType</c>（默认 application/json）；<c>TimeoutSeconds</c>（默认 30）；
/// <c>ResultVariable</c>（响应体写入的变量名，JSON 响应自动解析）；
/// <c>FailOnErrorStatus</c>（非 2xx 是否故障，默认 true，配合节点重试策略实现失败重试）。
/// 输出：<c>httpStatusCode</c>（响应状态码）与 ResultVariable 指定的响应内容。
/// </remarks>
[WorkflowActivity(WorkflowActivityTypes.Http, DisplayName = "HTTP 请求", Category = "集成")]
public class HttpRequestActivity : WorkflowActivityBase
{
    /// <summary>
    /// 命名 HttpClient 名称
    /// </summary>
    public const string HttpClientName = "XiHanWorkflow";

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        var url = await GetTemplatedStringAsync(context, "Url");
        if (string.IsNullOrWhiteSpace(url))
        {
            return ActivityExecutionResult.Fault($"HTTP 节点 {context.Node.Id} 未配置 Url");
        }

        var methodText = GetProperty<string>(context, "Method");
        var method = string.IsNullOrWhiteSpace(methodText) ? HttpMethod.Get : HttpMethod.Parse(methodText);
        var timeoutSeconds = GetProperty<int?>(context, "TimeoutSeconds") ?? 30;
        var contentType = GetProperty<string>(context, "ContentType") ?? "application/json";
        var resultVariable = GetProperty<string>(context, "ResultVariable");
        var failOnErrorStatus = GetProperty<bool?>(context, "FailOnErrorStatus") ?? true;
        var evaluator = GetEvaluator(context);

        using var request = new HttpRequestMessage(method, url);

        // 请求头分两类：内容头（Content-*）须挂在请求体上，先暂存待请求体构建后再附加
        var contentHeaders = new List<KeyValuePair<string, string>>();
        var headers = GetProperty<Dictionary<string, string>>(context, "Headers");
        if (headers is not null)
        {
            foreach (var pair in headers)
            {
                var value = await evaluator.RenderTemplateAsync(pair.Value, context.Variables.AsReadOnly, context.CancellationToken);
                if (!request.Headers.TryAddWithoutValidation(pair.Key, value))
                {
                    contentHeaders.Add(new KeyValuePair<string, string>(pair.Key, value));
                }
            }
        }

        if (context.Node.Properties.TryGetValue("Body", out var rawBody) && rawBody is not null)
        {
            string bodyText;
            if (WorkflowValueConverter.Normalize(rawBody) is string bodyTemplate)
            {
                bodyText = await evaluator.RenderTemplateAsync(bodyTemplate, context.Variables.AsReadOnly, context.CancellationToken);
            }
            else
            {
                bodyText = JsonSerializer.Serialize(WorkflowValueConverter.Normalize(rawBody), JsonSerializerOptions.Web);
            }

            request.Content = new StringContent(bodyText, Encoding.UTF8, contentType);

            foreach (var pair in contentHeaders)
            {
                request.Content.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
            }
        }

        var httpClientFactory = context.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(HttpClientName);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        HttpResponseMessage response;
        string responseText;
        try
        {
            response = await httpClient.SendAsync(request, timeoutCts.Token);
            responseText = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            return ActivityExecutionResult.Fault($"HTTP 节点 {context.Node.Id} 请求超时（{timeoutSeconds} 秒）：{url}");
        }
        catch (HttpRequestException ex)
        {
            return ActivityExecutionResult.Fault($"HTTP 节点 {context.Node.Id} 请求失败：{ex.Message}");
        }

        using (response)
        {
            var statusCode = (int)response.StatusCode;
            var outputs = new Dictionary<string, object?>
            {
                ["httpStatusCode"] = statusCode
            };

            if (failOnErrorStatus && !response.IsSuccessStatusCode)
            {
                return ActivityExecutionResult.Fault(
                    $"HTTP 节点 {context.Node.Id} 收到错误状态码 {statusCode}：{Truncate(responseText, 500)}");
            }

            if (!string.IsNullOrWhiteSpace(resultVariable))
            {
                outputs[resultVariable] = ParseResponse(responseText);
            }

            return ActivityExecutionResult.Complete(outputs);
        }
    }

    private static object? ParseResponse(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return null;
        }

        var trimmed = responseText.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            try
            {
                using var document = JsonDocument.Parse(responseText);
                return WorkflowValueConverter.Normalize(document.RootElement.Clone());
            }
            catch (JsonException)
            {
                // 非法 JSON 按原文返回
            }
        }

        return responseText;
    }

    private static string Truncate(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
