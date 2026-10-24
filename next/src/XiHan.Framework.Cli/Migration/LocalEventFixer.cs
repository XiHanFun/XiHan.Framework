// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// V4-08：<c>ILocalEventHandler&lt;TEvent&gt;</c> → <c>IMessageHandler</c>（每个事件一个主题常量，生成按主题解码并转调保留的 <c>HandleEventAsync</c> 的 <c>HandleAsync</c>，按主题登记），
/// <c>ILocalEventBus</c> → <c>IMessagePublisher</c>，<c>PublishAsync(evt)</c> → 带主题与信封的 <c>PublishAsync</c>，<c>Subscribe</c> 留标记。
/// </summary>
internal sealed class LocalEventFixer : SyntaxFixer
{
    /// <summary>
    /// 改写后要引入的命名空间。
    /// </summary>
    private static readonly string[] _usings = ["System.Text.Json", "XiHan.Framework.Messaging.Abstractions", "XiHan.Framework.Primitives"];

    /// <inheritdoc />
    public override string Id => "V4-08";

    /// <inheritdoc />
    protected override CompilationUnitSyntax Rewrite(CompilationUnitSyntax root, MigrationFile file, MigrationSession session)
    {
        var handlers = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(static declaration => EventTypes(declaration).Count > 0).ToHashSet();
        var usesBus = root.DescendantNodes().OfType<IdentifierNameSyntax>().Any(static name => name.Identifier.ValueText == "ILocalEventBus")
            || root.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(static invocation => IsBusCall(invocation, out _));
        var raises = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(static invocation => AggregateEventCall(invocation) is not null);
        if (handlers.Count == 0 && !usesBus && !raises)
        {
            return root;
        }

        var rewriter = new Rewriter(this, file, session, handlers);
        var result = (CompilationUnitSyntax)rewriter.Visit(root);
        return rewriter.NeedsUsings ? WithUsings(result, _usings, file.NewLine) : result;
    }

    /// <summary>
    /// 聚合根里 4.x 的 <c>AddLocalEvent(...)</c> / <c>AddDistributedEvent(...)</c>（接收者是 this 或省略）→ 方法名标识符；不是则为空。
    /// 不依赖同文件里有聚合根声明：partial 类的另一个文件也要改。
    /// </summary>
    private static SimpleNameSyntax? AggregateEventCall(InvocationExpressionSyntax invocation)
        => invocation.Expression switch
        {
            IdentifierNameSyntax { Identifier.ValueText: "AddLocalEvent" or "AddDistributedEvent" } name => name,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: { Identifier.ValueText: "AddLocalEvent" or "AddDistributedEvent" } name } => name,
            _ => null,
        };

    /// <summary>
    /// 类实现的 <c>ILocalEventHandler&lt;T&gt;</c> 的事件类型（源码写法）。
    /// </summary>
    internal static List<string> EventTypes(ClassDeclarationSyntax declaration)
        => declaration.BaseList?.Types.Select(static type => Unqualified(type.Type)).OfType<GenericNameSyntax>()
            .Where(static generic => generic.Identifier.ValueText == "ILocalEventHandler" && generic.TypeArgumentList.Arguments.Count == 1)
            .Select(static generic => generic.TypeArgumentList.Arguments[0].ToString())
            .ToList() ?? [];

    /// <summary>
    /// 事件类型 → 主题：类型名拆词、去掉尾部的 <c>event</c> 与 <c>domain</c>，用 <c>-</c> 连接。
    /// </summary>
    internal static string Topic(string eventType)
    {
        var words = V4Operations.Words(SimpleName(SyntaxFactory.ParseTypeName(eventType)));
        if (words.Count > 1 && words[^1] == "event")
        {
            words.RemoveAt(words.Count - 1);
        }

        if (words.Count > 1 && words[^1] == "domain")
        {
            words.RemoveAt(words.Count - 1);
        }

        return words.Count == 0 ? "event" : V4Operations.Kebab(words);
    }

    /// <summary>
    /// 事件类型 → 主题常量名：去掉 <c>DomainEvent</c> / <c>Event</c> 后缀加 <c>Topic</c>。
    /// </summary>
    internal static string TopicConstant(string eventType)
    {
        var name = SimpleName(SyntaxFactory.ParseTypeName(eventType));
        foreach (var suffix in new[] { "DomainEvent", "Event" })
        {
            if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
            {
                name = name[..^suffix.Length];
                break;
            }
        }

        return name + "Topic";
    }

    /// <summary>
    /// 去掉限定名前缀。
    /// </summary>
    private static TypeSyntax Unqualified(TypeSyntax type) => type is QualifiedNameSyntax qualified ? Unqualified(qualified.Right) : type;

    /// <summary>
    /// 是否对名字含 EventBus 的接收者调 <c>PublishAsync</c> / <c>Subscribe</c>。
    /// </summary>
    private static bool IsBusCall(InvocationExpressionSyntax invocation, out string method)
    {
        method = string.Empty;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "PublishAsync" or "Subscribe" } access
            || !access.Expression.ToString().Contains("EventBus", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        method = access.Name.Identifier.ValueText;
        return true;
    }

    /// <summary>
    /// 逐节点改写。
    /// </summary>
    private sealed class Rewriter(LocalEventFixer fixer, MigrationFile file, MigrationSession session, HashSet<ClassDeclarationSyntax> handlers) : CSharpSyntaxRewriter
    {
        /// <summary>
        /// 要插在原语句或成员前的标记。
        /// </summary>
        private readonly Dictionary<SyntaxNode, List<string>> _pending = [];

        /// <summary>
        /// 项目末段的 kebab 形式（信封 Source），懒算。
        /// </summary>
        private string? _source;

        /// <summary>
        /// 是否改了任何东西。
        /// </summary>
        public bool Changed { get; private set; }

        /// <summary>
        /// 是否改了处理器或总线调用（这些改写用到消息与 JSON 的命名空间；只改 <c>Raise</c> 不补 using）。
        /// </summary>
        public bool NeedsUsings { get; private set; }

        /// <inheritdoc />
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (node.Identifier.ValueText != "ILocalEventBus")
            {
                return base.VisitIdentifierName(node);
            }

            Changed = true;
            NeedsUsings = true;
            session.Changed(fixer.Id, file, LineOf(node));
            return node.WithIdentifier(SyntaxFactory.Identifier(node.Identifier.LeadingTrivia, "IMessagePublisher", node.Identifier.TrailingTrivia));
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var visited = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
            if (!handlers.Contains(node))
            {
                return visited;
            }

            var events = EventTypes(node);
            var className = node.Identifier.ValueText;
            Changed = true;
            NeedsUsings = true;
            session.Changed(fixer.Id, file, LineOf(node));

            // 基类：去掉全部 ILocalEventHandler<T>，加一个 IMessageHandler
            var kept = visited.BaseList!.Types.Where(static type => Unqualified(type.Type) is not GenericNameSyntax { Identifier.ValueText: "ILocalEventHandler" }).ToList();
            if (!kept.Any(static type => SimpleName(type.Type) == "IMessageHandler"))
            {
                kept.Insert(0, SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName("IMessageHandler")));
            }

            visited = visited.WithBaseList(RebuildBaseList(visited.BaseList, kept));

            // 成员：主题常量 + 按主题解码转调的 HandleAsync，放在最前面；原 HandleEventAsync 保留
            var newLine = file.NewLine;
            var indent = IndentOf(node) + "    ";
            var generated = CliMessages.MigrateGenerated.L();
            var decode = CliMessages.MigrateMarkerEventJson.L();
            var text = new StringBuilder();
            foreach (var eventType in events)
            {
                text.Append(indent).Append("/// <summary>").Append(generated).Append("</summary>").Append(newLine);
                text.Append(indent).Append("public const string ").Append(TopicConstant(eventType)).Append(" = \"").Append(Topic(eventType)).Append("\";").Append(newLine).Append(newLine);
            }

            text.Append(indent).Append("/// <inheritdoc />").Append(newLine);
            text.Append(indent).Append("public async ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken)").Append(newLine);
            text.Append(indent).Append('{').Append(newLine);
            text.Append(indent).Append("    ").Append(Markers.Line(fixer.Id, decode)).Append(newLine);
            if (events.Count == 1)
            {
                text.Append(indent).Append("    await HandleEventAsync(JsonSerializer.Deserialize<").Append(events[0]).Append(">(delivery.Envelope.Data.Span)!).ConfigureAwait(false);").Append(newLine);
                text.Append(indent).Append("    return Result.Success();").Append(newLine);
            }
            else
            {
                text.Append(indent).Append("    switch (delivery.Topic)").Append(newLine);
                text.Append(indent).Append("    {").Append(newLine);
                foreach (var eventType in events)
                {
                    text.Append(indent).Append("        case ").Append(TopicConstant(eventType)).Append(':').Append(newLine);
                    text.Append(indent).Append("            await HandleEventAsync(JsonSerializer.Deserialize<").Append(eventType).Append(">(delivery.Envelope.Data.Span)!).ConfigureAwait(false);").Append(newLine);
                    text.Append(indent).Append("            return Result.Success();").Append(newLine);
                }

                text.Append(indent).Append("        default:").Append(newLine);
                text.Append(indent).Append("            return Result.Success();").Append(newLine);
                text.Append(indent).Append("    }").Append(newLine);
            }

            text.Append(indent).Append('}').Append(newLine);
            session.Marked(fixer.Id, file, LineOf(node), decode);
            var members = ParseMembers(text.ToString(), newLine).ToList();
            if (visited.Members.Count > 0 && !StartsWithBlankLine(visited.Members[0]))
            {
                members[^1] = members[^1].WithTrailingTrivia(members[^1].GetTrailingTrivia().Add(SyntaxFactory.EndOfLine(newLine)));
            }

            visited = visited.WithMembers(SyntaxFactory.List(members.Concat(visited.Members)));

            // 按主题登记
            if (file.ProjectDirectory is not null)
            {
                if (!session.Registrations.TryGetValue(file.ProjectDirectory, out var registrations))
                {
                    registrations = [];
                    session.Registrations[file.ProjectDirectory] = registrations;
                }

                var ns = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? string.Empty;
                registrations.RemoveAll(registration => registration.Implementation == className);
                registrations.Add(new V4ServiceRegistration(ns, className, "Scoped", [], events.Select(eventType => $"{className}.{TopicConstant(eventType)}").ToList()));
            }

            return visited;
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            if (AggregateEventCall(node) is { } eventCall)
            {
                // 聚合根缓冲领域事件：4.x 分本地/分布式两个方法，5.0 只有 Raise
                Changed = true;
                session.Changed(fixer.Id, file, LineOf(node));
                var raise = SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(eventCall.Identifier.LeadingTrivia, "Raise", eventCall.Identifier.TrailingTrivia));
                return visited.Expression switch
                {
                    MemberAccessExpressionSyntax access => visited.WithExpression(access.WithName(raise)),
                    _ => visited.WithExpression(raise),
                };
            }

            if (!IsBusCall(node, out var method))
            {
                return visited;
            }

            var anchor = node.Ancestors().FirstOrDefault(static ancestor => ancestor is ExpressionStatementSyntax or LocalDeclarationStatementSyntax or ReturnStatementSyntax or MemberDeclarationSyntax);
            if (method == "Subscribe")
            {
                Pend(anchor, CliMessages.MigrateMarkerEventSubscribe.L());
                return visited;
            }

            // 只改 4.x 形态：PublishAsync(evt) 或 PublishAsync(evt, ct)
            var arguments = node.ArgumentList.Arguments;
            if (arguments.Count == 0 || arguments.Count > 2 || (arguments.Count == 2 && !LooksLikeToken(arguments[1].Expression)))
            {
                return visited;
            }

            var payload = arguments[0].Expression;
            var eventType = payload switch
            {
                ObjectCreationExpressionSyntax creation => creation.Type.ToString(),
                IdentifierNameSyntax identifier => DeclaredType(node, identifier.Identifier.ValueText),
                _ => null,
            };
            string topic;
            if (eventType is null)
            {
                topic = payload is IdentifierNameSyntax identifier ? V4Operations.Kebab(V4Operations.Words(identifier.Identifier.ValueText)) : "event";
                Pend(anchor, CliMessages.MigrateMarkerEventTopicUnresolved.L(("name", payload.ToString())));
            }
            else
            {
                topic = Topic(eventType);
            }

            var token = arguments.Count == 2 ? arguments[1].Expression.ToString() : CancellationTokenFor(node);
            var data = visited.ArgumentList.Arguments[0].Expression.ToString();
            var text = $"(\"{topic}\", new MessageEnvelope {{ Id = Guid.NewGuid().ToString(\"N\"), Source = \"{Source()}\", Type = \"{topic}\", Time = DateTimeOffset.UtcNow, Data = JsonSerializer.SerializeToUtf8Bytes({data}) }}, {token})";
            Changed = true;
            NeedsUsings = true;
            session.Changed(fixer.Id, file, LineOf(node));
            Pend(anchor, CliMessages.MigrateMarkerEventJson.L());
            return visited.WithArgumentList(SyntaxFactory.ParseArgumentList(text).WithTriviaFrom(visited.ArgumentList));
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node) => Apply(base.VisitExpressionStatement(node), node);

        /// <inheritdoc />
        public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node) => Apply(base.VisitLocalDeclarationStatement(node), node);

        /// <inheritdoc />
        public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node) => Apply(base.VisitReturnStatement(node), node);

        /// <inheritdoc />
        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node) => Apply(base.VisitMethodDeclaration(node), node);

        /// <inheritdoc />
        public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node) => Apply(base.VisitPropertyDeclaration(node), node);

        /// <summary>
        /// 记一个要插在原节点前的标记。
        /// </summary>
        private void Pend(SyntaxNode? anchor, string message)
        {
            if (anchor is null)
            {
                return;
            }

            if (!_pending.TryGetValue(anchor, out var messages))
            {
                messages = [];
                _pending[anchor] = messages;
            }

            if (!messages.Contains(message, StringComparer.Ordinal))
            {
                messages.Add(message);
            }
        }

        /// <summary>
        /// 把记下的标记插到改写后的节点前，行号取原节点。
        /// </summary>
        private SyntaxNode? Apply(SyntaxNode? visited, SyntaxNode original)
        {
            if (visited is null || !_pending.TryGetValue(original, out var messages))
            {
                return visited;
            }

            foreach (var message in messages)
            {
                if (Markers.Has(visited, fixer.Id, message))
                {
                    continue;
                }

                session.Marked(fixer.Id, file, LineOf(original), message);
                visited = Markers.With(visited, fixer.Id, message, file.NewLine);
            }

            return visited;
        }

        /// <summary>
        /// 信封 Source：<c>/&lt;项目末段 kebab&gt;</c>。
        /// </summary>
        private string Source()
        {
            if (_source is null)
            {
                var stem = file.ProjectDirectory is null ? "app" : V4OperationsGenerator.Stem(V4ServicesGenerator.Names(file.ProjectDirectory).Project);
                _source = "/" + V4Operations.Kebab(V4Operations.Words(stem));
            }

            return _source;
        }

        /// <summary>
        /// 表达式是否像取消令牌。
        /// </summary>
        private static bool LooksLikeToken(ExpressionSyntax expression)
        {
            var text = expression.ToString();
            return text.Contains("cancellation", StringComparison.OrdinalIgnoreCase) || text.Contains("token", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 所在方法或 lambda 的 <c>CancellationToken</c> 参数名；没有用 <c>CancellationToken.None</c>。
        /// </summary>
        private static string CancellationTokenFor(SyntaxNode node)
        {
            foreach (var ancestor in node.Ancestors())
            {
                var parameters = ancestor switch
                {
                    MethodDeclarationSyntax method => method.ParameterList.Parameters,
                    LocalFunctionStatementSyntax local => local.ParameterList.Parameters,
                    ParenthesizedLambdaExpressionSyntax lambda => lambda.ParameterList.Parameters,
                    _ => default,
                };
                var token = parameters.FirstOrDefault(static parameter => parameter.Type is not null && SimpleName(parameter.Type) == "CancellationToken");
                if (token is not null)
                {
                    return token.Identifier.ValueText;
                }

                if (ancestor is MethodDeclarationSyntax)
                {
                    break;
                }
            }

            return "CancellationToken.None";
        }

        /// <summary>
        /// 同一成员里名为 <paramref name="name"/> 的局部变量或参数的声明类型（<c>var</c> 看初始化器的 <c>new T(...)</c>）。
        /// </summary>
        private static string? DeclaredType(SyntaxNode node, string name)
        {
            var member = node.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault();
            if (member is null)
            {
                return null;
            }

            foreach (var declarator in member.DescendantNodes().OfType<VariableDeclaratorSyntax>().Where(declarator => declarator.Identifier.ValueText == name))
            {
                if (declarator.Parent is VariableDeclarationSyntax { Type: var type } && !type.IsVar)
                {
                    return type.ToString();
                }

                if (declarator.Initializer?.Value is ObjectCreationExpressionSyntax creation)
                {
                    return creation.Type.ToString();
                }
            }

            var parameter = member.DescendantNodes().OfType<ParameterSyntax>().FirstOrDefault(parameter => parameter.Identifier.ValueText == name && parameter.Type is not null);
            return parameter?.Type?.ToString();
        }
    }
}
