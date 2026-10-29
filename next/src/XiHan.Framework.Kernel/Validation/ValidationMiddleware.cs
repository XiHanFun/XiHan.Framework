// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Text.Json;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Validation;

/// <summary>
/// Validate 阶段的内置中间件（RFC-0011）。
/// </summary>
/// <remarks>
/// 对带操作描述的调用，先按契约的请求 JSON Schema 校验载荷（<see cref="KernelErrors.SchemaViolation"/>），再解码并对实现 <see cref="IValidatableObject"/> 的请求执行自校验（<see cref="KernelErrors.RequestInvalid"/>）；不通过即以 VALIDATION 结果结束调用，不进 Handle。解码好的请求记录在上下文，Handle 阶段直接执行。没有操作描述的调用原样透传。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ValidationMiddleware : IPipelineMiddleware
{
    /// <summary>
    /// 空载荷视为空对象，与解码规则一致。
    /// </summary>
    private static readonly byte[] _emptyObject = "{}"u8.ToArray();

    /// <summary>
    /// 每个 <see cref="OperationSchema"/> 解析一次的请求 Schema 文档。
    /// </summary>
    /// <remarks>
    /// 描述随登记表活到应用结束，弱表让卸载的插件描述可回收。
    /// </remarks>
    private readonly ConditionalWeakTable<OperationSchema, JsonDocument> _schemas = [];

    /// <inheritdoc />
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);
        if (!context.GetOperation().TryGetValue(out var descriptor))
        {
            await nextStep(context).ConfigureAwait(false);
            return;
        }

        var request = context.GetOperationRequest().TryGetValue(out var present) ? present : null;
        // JSON 载荷：原始调用用上下文里的载荷；类型化调用只有对象，按描述的序列化元数据编码一次；都没有（宿主端点自己绑定、插件代理的类型化调用）就跳过 Schema 校验与解码。
        // 分支显式写开：条件表达式会把 null 的 byte[] 转成空的 ReadOnlyMemory（有值），那会被当成 {} 解码
        ReadOnlyMemory<byte>? payload = null;
        if (context.GetOperationPayload().TryGetValue(out var raw))
        {
            payload = raw;
        }
        else if (request is not null && descriptor.RequestInfo is { } info)
        {
            payload = JsonSerializer.SerializeToUtf8Bytes(request, info);
        }

        if (payload is { } bytes && descriptor.Schema is { } schema)
        {
            var outcome = ValidateSchema(descriptor, schema, bytes);
            if (outcome is not null)
            {
                context.SetOutcome(outcome);
                return;
            }
        }

        // 原始调用在这里解码：载荷不合法与 Schema 违规一样在 Validate 阶段拒绝，Handle 阶段拿到的一定是合法对象
        if (request is null && payload is { } undecoded)
        {
            var decoded = descriptor.DecodeRequest(undecoded);
            if (!decoded.TryGetValue(out request))
            {
                context.SetOutcome(decoded.Error!);
                return;
            }

            context.SetOperationRequest(request);
        }

        if (request is IValidatableObject validatable && SelfValidate(descriptor, validatable, context) is { } invalid)
        {
            context.SetOutcome(invalid);
            return;
        }

        await nextStep(context).ConfigureAwait(false);
    }

    /// <summary>
    /// 按请求 Schema 校验载荷。
    /// </summary>
    /// <remarks>
    /// 不是 JSON 返回 <see cref="KernelErrors.InvalidOperationPayload"/>，有违规返回 <see cref="KernelErrors.SchemaViolation"/>，通过返回 <see langword="null"/>。
    /// </remarks>
    private Error? ValidateSchema(OperationDescriptor descriptor, OperationSchema schema, ReadOnlyMemory<byte> payload)
    {
        JsonDocument instance;
        try
        {
            instance = JsonDocument.Parse(payload.IsEmpty ? _emptyObject : payload);
        }
        catch (JsonException exception)
        {
            return Error.Validation(KernelErrors.InvalidOperationPayload, KernelMessages.InvalidOperationPayload, MessageArguments.Of(("id", descriptor.Id), ("reason", exception.Message)));
        }

        using (instance)
        {
            // 同一描述的 Schema 只解析一次；解析失败是登记时就该发现的编程错误，OperationSchema 构造已校验过是 JSON 对象
            var compiled = _schemas.GetValue(schema, static s => JsonDocument.Parse(s.Request));
            var violations = JsonSchemaValidator.Validate(compiled.RootElement, instance.RootElement);
            return violations.Count == 0 ? null : Error.Validation(KernelErrors.SchemaViolation, KernelMessages.SchemaViolation, Details(descriptor.Id, violations));
        }
    }

    /// <summary>
    /// 执行请求对象的自校验。
    /// </summary>
    /// <remarks>
    /// 上下文带请求类型名与调用作用域的服务提供器，不经反射。
    /// </remarks>
    private static Error? SelfValidate(OperationDescriptor descriptor, IValidatableObject request, InvocationContext context)
    {
        var violations = new List<SchemaViolation>();
        var validation = new ValidationContext(request, descriptor.RequestType.Name, context.Services, items: null);
        foreach (var result in request.Validate(validation))
        {
            if (result is null || result == ValidationResult.Success)
            {
                continue;
            }

            // 一条结果可能涉及多个成员：按成员各列一条，没有成员的挂在根上；应用给的文案原样进原因（应用在 Validate 里用 key.L() 即得调用方语言），
            // 没有文案时用内核的兜底键让宿主翻译；成员名是 CLR 属性名，按 camelCase 转成序列化名，让字段级错误与请求 JSON 的键一致
            var hasMessage = !string.IsNullOrWhiteSpace(result.ErrorMessage);
            var reason = hasMessage ? result.ErrorMessage! : "value is invalid";
            var messageKey = hasMessage ? null : KernelMessages.ValidationInvalid;
            var members = result.MemberNames.Where(static m => !string.IsNullOrWhiteSpace(m)).ToList();
            if (members.Count == 0)
            {
                violations.Add(new SchemaViolation("$", reason, messageKey));
            }

            foreach (var member in members)
            {
                violations.Add(new SchemaViolation($"$.{JsonNamingPolicy.CamelCase.ConvertName(member)}", reason, messageKey));
            }

            if (violations.Count >= JsonSchemaValidator.MaxViolations)
            {
                break;
            }
        }

        return violations.Count == 0 ? null : Error.Validation(KernelErrors.RequestInvalid, KernelMessages.RequestInvalid, Details(descriptor.Id, violations));
    }

    /// <summary>
    /// 失败结果的细节。
    /// </summary>
    /// <remarks>
    /// <c>id</c> 是操作，<c>reason</c> 是第一条违规，<c>violations</c> 是全部的 <c>路径: 原因</c> 文本，<c>errors</c> 是全部违规对象（宿主据此写字段级错误）。
    /// </remarks>
    private static IReadOnlyDictionary<string, object?> Details(string operationId, IReadOnlyList<SchemaViolation> violations)
        => MessageArguments.Of(("id", operationId), ("reason", violations[0].ToString()), ("violations", violations.Select(static v => v.ToString()).ToList()), ("errors", violations));
}
