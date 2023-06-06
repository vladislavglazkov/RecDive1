using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.IO;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;
using System.Runtime;
namespace RecDive1.CodeAnalyze
{

    internal class RewriterFactory
    {
        private List<CSharpSyntaxRewriter> rewriters;

        public RewriterFactory()
        {

            rewriters = new List<CSharpSyntaxRewriter>();
        }

        public RewriterFactory Add(CSharpSyntaxRewriter rewriter)
        {
            rewriters.Add(rewriter);
            return this;
        }

        public SyntaxNode Apply(SyntaxNode root)
        {
            foreach (var rewriter in rewriters)
            {
                root = rewriter.Visit(root);
            }

            return root;
        }
    }
    internal abstract class CustomRewriter : CSharpSyntaxRewriter
    {
        protected StatementSyntax newStatement;

        public CustomRewriter(StatementSyntax newStatement)
        {
            this.newStatement = newStatement;
        }
    }
    internal class RewriterStart : CustomRewriter
    {
        public RewriterStart(StatementSyntax newStatement) : base(newStatement)
        {
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (!node.Modifiers.Any(SyntaxKind.AbstractKeyword))
            {
                // Adding new statement at the end of the method but before all return instructions
                var statements = node.Body.Statements;
                var returnStatements = statements.OfType<ReturnStatementSyntax>().ToList();

                var newStatements = (new List<StatementSyntax> { newStatement }).Concat(statements)
                    .ToArray();
                return node.WithBody(node.Body.WithStatements(SyntaxFactory.List(newStatements)));
            }
            return node;
        }
    }
    internal class RewriterEnd : CustomRewriter
    {
        public RewriterEnd(StatementSyntax newStatement) : base(newStatement)
        {
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            // Adding new statement at the end of the method but before all return instructions
            if (!node.Modifiers.Any(SyntaxKind.AbstractKeyword))
            {

                var statements = node.Body.Statements;
                var returnStatements = statements.OfType<ReturnStatementSyntax>().ToList();

                var newStatements = statements
                    .Concat(new List<StatementSyntax> { newStatement })
                    .ToArray();
                return node.WithBody(node.Body.WithStatements(SyntaxFactory.List(newStatements)));

            }
            return node;
        }
    }
    internal class RewriterReturn : CustomRewriter
    {
        public RewriterReturn(StatementSyntax newStatement) : base(newStatement)
        {
        }

        public override SyntaxNode VisitReturnStatement(ReturnStatementSyntax node)
        {
            // Add new statement before return
            var newStatements = new[] { newStatement, node };
            return SyntaxFactory.Block(newStatements);
        }

    }

    internal class MethodDeclarationSyntaxWalker : CSharpSyntaxWalker
    {
        public List<MethodDeclarationSyntax> Methods { get; } = new List<MethodDeclarationSyntax>();

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (node.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                Methods.Add(node);
            }
            base.VisitMethodDeclaration(node);
        }
    }



    public class CodeComponent
    {
        private SyntaxTree tree;
        private static SyntaxTree ConvertTree(SyntaxTree tree,Guid guid)
        {
            var root = tree.GetCompilationUnitRoot();
            var stmt = SyntaxFactory.ParseStatement($"Metrics.Metrics.Increment(Guid.Parse(\"{guid}\"));");
            var stmt1 = SyntaxFactory.ParseStatement($"Metrics.Metrics.Decrement(Guid.Parse(\"{guid}\"));");
           
            var rwr = new RewriterFactory().Add(new RewriterReturn(stmt1)).Add(new RewriterEnd(stmt1)).Add(new RewriterStart(stmt));
            var nroot = rwr.Apply(root);

            var ntree = SyntaxFactory.SyntaxTree(nroot);
            return ntree;
        }
        public static CodeComponent Load(string code)
        {
            try
            {
                SyntaxTree tree = CSharpSyntaxTree.ParseText(code);

                return new CodeComponent { tree = tree };
                

            }
            catch
            {
                throw new ArgumentException("Provided code is not valid.");

            }
        }
        public List<MethodDeclarationSyntax> GetAllMethods()
        {
            MethodDeclarationSyntaxWalker walker = new MethodDeclarationSyntaxWalker();
            walker.Visit(tree.GetCompilationUnitRoot());
            return walker.Methods;
        }



        public static string GetFullyQualifiedName(MethodDeclarationSyntax method)
        {
            //var parentNamespace = method.AncestorsAndSelf().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            var parentClass = method.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();

            return $"{parentClass.Identifier.Text}.{method.Identifier.Text}";
        }
        public static string GetFullMethodSignature(MethodDeclarationSyntax method)
        {
            var parentClass = method.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().First();

            string methodName = method.Identifier.Text;
            string className = parentClass.Identifier.Text;
            string parameters = string.Join(", ", method.ParameterList.Parameters.Select(p => $"{p.Type} {p.Identifier}"));

            return $"{className}.{methodName}({parameters})";
        }

        public int GetRes(string _class,string _method,object[] args)
        {

            Guid sessionGuid = Guid.NewGuid();
            Metrics.Metrics.Init(sessionGuid);
            var tree = ConvertTree(this.tree, sessionGuid);

            var compileOptions = new CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary);

            List<MetadataReference> metaDatas = new List<MetadataReference>();
            string path = @"C:\Users\vladi\source\repos\RecDiveSample\RecDiveSample\bin\Debug\";
            metaDatas.Add(MetadataReference.CreateFromFile(path+"System.dll"));

            metaDatas.Add(MetadataReference.CreateFromFile(path+"mscorlib.dll"));
            metaDatas.Add(MetadataReference.CreateFromFile(path+"Metrics.dll"));
            metaDatas.Add(MetadataReference.CreateFromFile(path+"System.Drawing.dll"));


            var compilation = CSharpCompilation.Create("assembly.dll", new List<SyntaxTree> { tree }, metaDatas, compileOptions);
            MemoryStream ms = new MemoryStream();

            var ress = compilation.Emit(ms);
            Assembly asm = Assembly.Load(ms.GetBuffer());

            var ops = asm.GetType(_class);
            //var obj = Activator.CreateInstance(ops);
            var meth = ops.GetMethod(_method);
            meth.Invoke(null, args);

            var count = Metrics.Metrics.GetLocMax(sessionGuid);
            Metrics.Metrics.Clear(sessionGuid);

            return count;
        }
        
    }
}