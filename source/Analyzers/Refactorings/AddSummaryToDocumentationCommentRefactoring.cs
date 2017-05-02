﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Roslynator.CSharp;
using Roslynator.Utilities;

namespace Roslynator.CSharp.Refactorings
{
    internal static class AddSummaryToDocumentationCommentRefactoring
    {
        public static void AnalyzeSingleLineDocumentationCommentTrivia(SyntaxNodeAnalysisContext context)
        {
            var documentationComment = (DocumentationCommentTriviaSyntax)context.Node;

            bool containsInheritDoc = false;
            bool containsIncludeOrExclude = false;
            bool containsSummaryElement = false;
            bool isFirst = true;

            foreach (XmlNodeSyntax node in documentationComment.Content)
            {
                XmlElementInfo info;
                if (XmlElementInfo.TryCreate(node, out info))
                {
                    switch (info.ElementKind)
                    {
                        case XmlElementKind.Include:
                        case XmlElementKind.Exclude:
                            {
                                if (isFirst)
                                    containsIncludeOrExclude = true;

                                break;
                            }
                        case XmlElementKind.InheritDoc:
                            {
                                containsInheritDoc = true;
                                break;
                            }
                        case XmlElementKind.Summary:
                            {
                                if (info.IsXmlEmptyElement || IsSummaryMissing((XmlElementSyntax)info.Element))
                                {
                                    context.ReportDiagnostic(
                                        DiagnosticDescriptors.AddSummaryToDocumentationComment,
                                        info.Element);
                                }

                                containsSummaryElement = true;
                                break;
                            }
                    }

                    if (isFirst)
                    {
                        isFirst = false;
                    }
                    else
                    {
                        containsIncludeOrExclude = false;
                    }

                    if (containsInheritDoc && containsSummaryElement)
                        break;
                }
            }

            if (!containsSummaryElement
                && !containsInheritDoc
                && !containsIncludeOrExclude)
            {
                context.ReportDiagnostic(
                    DiagnosticDescriptors.AddSummaryElementToDocumentationComment,
                    documentationComment);
            }
        }

        private static bool IsSummaryMissing(XmlElementSyntax summaryElement)
        {
            SyntaxList<XmlNodeSyntax> content = summaryElement.Content;

            if (content.Count == 0)
            {
                return true;
            }
            else if (content.Count == 1)
            {
                XmlNodeSyntax node = content.First();

                if (node.IsKind(SyntaxKind.XmlText))
                {
                    var xmlText = (XmlTextSyntax)node;

                    return xmlText.TextTokens.All(f => IsWhitespaceOrNewLine(f));
                }
            }

            return false;
        }

        private static bool IsWhitespaceOrNewLine(SyntaxToken token)
        {
            switch (token.Kind())
            {
                case SyntaxKind.XmlTextLiteralNewLineToken:
                    return true;
                case SyntaxKind.XmlTextLiteralToken:
                    return string.IsNullOrWhiteSpace(token.ValueText);
                default:
                    return false;
            }
        }

        public static async Task<Document> RefactorAsync(
            Document document,
            DocumentationCommentTriviaSyntax documentationComment,
            CancellationToken cancellationToken)
        {
            XmlElementSyntax summaryElement = documentationComment.SummaryElement();

            if (summaryElement == null)
            {
                SyntaxList<XmlNodeSyntax> content = documentationComment.Content;

                SourceText text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);

                TextLine line = text.Lines[documentationComment.GetFullSpanStartLine(cancellationToken)];

                string indent = StringUtility.GetIndent(line.ToString());

                string newText = CreateSummaryElement(indent);

                var textChange = new TextChange(new TextSpan(documentationComment.FullSpan.Start, 0), newText);

                return await document.WithTextChangeAsync(textChange).ConfigureAwait(false);
            }

            Debug.Fail("");

            return document;
        }

        private static string CreateSummaryElement(string indent)
        {
            var sb = new StringBuilder();

            sb.AppendLine("/// <summary>");
            sb.Append(indent);
            sb.AppendLine("/// ");
            sb.Append(indent);
            sb.AppendLine("/// </summary>");
            sb.Append(indent);

            return sb.ToString();
        }
    }
}