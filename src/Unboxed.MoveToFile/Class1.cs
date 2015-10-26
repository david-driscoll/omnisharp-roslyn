using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using OmniSharp.Services;

namespace Unboxed.MoveToFile
{
    [Export(typeof(ICodeActionProvider))]
    class UnboxedCodeActionProvider : ICodeActionProvider
    {
        public IEnumerable<Assembly> Assemblies
        {
            get
            {
                yield return typeof(UnboxedCodeActionProvider).GetTypeInfo().Assembly;
            }
        }

        public IEnumerable<CodeFixProvider> CodeFixes
        {
            get
            {
                return Enumerable.Empty<CodeFixProvider>();
            }
        }

        public IEnumerable<CodeRefactoringProvider> Refactorings
        {
            get
            {
                yield return new CodeRefactoring8CodeRefactoringProvider();
            }
        }
    }

    internal class CodeRefactoring8CodeRefactoringProvider : CodeRefactoringProvider
    {
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            // TODO: Replace the following code with your own analysis, generating a CodeAction for each refactoring to offer

            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // Find the node at the selection.
            var node = root.FindNode(context.Span);

            // Only offer a refactoring if the selected node is a type declaration node.
            var typeDecl = node as TypeDeclarationSyntax;
            if (typeDecl == null || $"{typeDecl.Identifier.ToString().ToLowerInvariant()}.cs" == context.Document.Name.ToLowerInvariant())
            {
                return;
            }

            // For any type declaration node, create a code action to reverse the identifier text.
            var action = CodeAction.Create("Move class to separate file", c => ReverseTypeNameAsync(context.Document, typeDecl, c));

            // Register this code action.
            context.RegisterRefactoring(action);
        }

        private async Task<Solution> ReverseTypeNameAsync(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            // Produce a reversed version of the type declaration's identifier token.
            var identifierToken = typeDecl.Identifier;

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.RemoveNode(typeDecl, SyntaxRemoveOptions.KeepNoTrivia);

            document = document.WithSyntaxRoot(newRoot);

            var newTree =
                SyntaxFactory.CompilationUnit()
                    .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(typeDecl))
                    .WithoutLeadingTrivia();

            var newDocument = document.Project.AddDocument($"{identifierToken}.cs", newTree, document.Folders);

            return newDocument.Project.Solution;
        }
    }
}
