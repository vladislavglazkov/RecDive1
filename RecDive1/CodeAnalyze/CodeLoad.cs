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
using System.Globalization;

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
    /*internal abstract class CustomRewriter : CSharpSyntaxRewriter
    {
        protected SyntaxNode newStatement;

        public CustomRewriter(StatementSyntax newStatement)
        {
            this.newStatement = newStatement;
        }
    }*/
    internal class RewriterStart : CSharpSyntaxRewriter
    {
        protected StatementSyntax newStatement;

        public RewriterStart(StatementSyntax newStatement) 
        {
            this.newStatement = newStatement;
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (!node.Modifiers.Any(SyntaxKind.AbstractKeyword))
            {
                // Adding new statement at the end of the method but before all return instructions
                var statements = node.Body.Statements;
                var returnStatements = statements.OfType<ReturnStatementSyntax>().ToList();

                var newStatements = (new List<SyntaxNode> { newStatement }).Concat(statements)
                    .ToArray();
                return node.WithBody(node.Body.WithStatements(SyntaxFactory.List(newStatements)));
            }
            return node;
        }
    }
    internal class RewriterEnd : CSharpSyntaxRewriter
    {
        protected StatementSyntax newStatement;
        public RewriterEnd(StatementSyntax newStatement)
        {
            this.newStatement=  newStatement;
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            // Adding new statement at the end of the method but before all return instructions
            if (!node.Modifiers.Any(SyntaxKind.AbstractKeyword))
            {

                var statements = node.Body.Statements;
                var returnStatements = statements.OfType<ReturnStatementSyntax>().ToList();

                var newStatements = statements
                    .Concat(new List<SyntaxNode> { newStatement })
                    .ToArray();
                return node.WithBody(node.Body.WithStatements(SyntaxFactory.List(newStatements)));

            }
            return node;
        }
    }
    internal class RewriterReturn : CSharpSyntaxRewriter
    {
        protected StatementSyntax newStatement;
        public RewriterReturn(StatementSyntax newStatement)
        {
            this.newStatement= newStatement;
        }

        public override SyntaxNode VisitReturnStatement(ReturnStatementSyntax node)
        {
            // Add new statement before return
            var newStatements = new[] { newStatement, node };
            return SyntaxFactory.Block((StatementSyntax[]) newStatements);
        }

    }
    internal class ConditionalReturnRewriter : CSharpSyntaxRewriter
    {
        protected ExpressionSyntax newStatement;
        public ConditionalReturnRewriter(ExpressionSyntax conditionStatement) { 
            this.newStatement= conditionStatement;
        }
        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (node.Body == null)
                return node;
            var statements = node.Body.Statements;
            var returnStatements = statements.OfType<ReturnStatementSyntax>().ToList();

            var isVoid = (node.ReturnType.Kind() == SyntaxKind.PredefinedType && node.ReturnType.ToString() == "void");
            var returnStmt = SyntaxFactory.ReturnStatement(isVoid?null:SyntaxFactory.DefaultExpression(node.ReturnType));
            var ifStmt =  SyntaxFactory.IfStatement((ExpressionSyntax)newStatement,returnStmt);
            var newStatements = new List<SyntaxNode> { ifStmt }
                .Concat(statements)
                .ToArray();
            return node.WithBody(node.Body.WithStatements(SyntaxFactory.List(newStatements)));
        }
    }
    internal class ReturnSeparateRewriter : CSharpSyntaxRewriter {
        //public override void visit
        SemanticModel semanticModel;
        public ReturnSeparateRewriter(SemanticModel semanticModel)
        {
            this.semanticModel= semanticModel;
        }
        private TypeSyntax GetMethodType(SyntaxNode node)
        {

            var act = (ReturnStatementSyntax)node;
            
            var dstr=semanticModel.GetTypeInfo(act.Expression).Type.ToDisplayString();
            return SyntaxFactory.ParseTypeName(dstr);
            /*if (node is MethodDeclarationSyntax)
            {
                return ((MethodDeclarationSyntax)node).ReturnType;
            }
            else if (node is PropertyDeclarationSyntax)
            {
                return ((PropertyDeclarationSyntax)node).Type;
            }*/


            return GetMethodType(node.Parent);
        }
        public override SyntaxNode VisitReturnStatement(ReturnStatementSyntax node)
        {
            if (node.Expression == null)
                return node;
            var list=new List<StatementSyntax>();
            var type = GetMethodType(node);
            var guid=Guid.NewGuid();
            var varname= "__rval" + guid.ToString("N");
            SeparatedSyntaxList<VariableDeclaratorSyntax> dlist = SyntaxFactory.SeparatedList<VariableDeclaratorSyntax>(new VariableDeclaratorSyntax[] { SyntaxFactory.VariableDeclarator(SyntaxFactory.ParseToken(varname),null,SyntaxFactory.EqualsValueClause(node.Expression)) } );
            list.Add(SyntaxFactory.LocalDeclarationStatement(SyntaxFactory.VariableDeclaration(type, dlist)));
            list.Add(SyntaxFactory.ReturnStatement(SyntaxFactory.ParseExpression(varname)));
            var finres = SyntaxFactory.Block(list);
            return finres;
        }
    }

    internal class MethodDeclarationSyntaxWalker : CSharpSyntaxWalker
    {
        public List<MethodDeclarationSyntax> Methods { get; } = new List<MethodDeclarationSyntax>();

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (!node.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                Methods.Add(node);
            }
            base.VisitMethodDeclaration(node);
        }
        
    }



    public class CodeComponent
    {
        private SyntaxTree tree;
        private static List<string> requestedAssemblies = new List<string> { "System", "mscorlib", "System.Core", "System.Drawing", "Metrics" };

        private static SyntaxTree ConvertTree(SyntaxTree tree,Guid guid)
        {

            var compileOptions = new CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary);


            List<MetadataReference> metaDatas = new List<MetadataReference>();
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var h in allAssemblies)
            {
                if (requestedAssemblies.Contains(h.GetName().Name))
                {
                    metaDatas.Add(MetadataReference.CreateFromFile(h.Location));
                }
            }

            var compilation = CSharpCompilation.Create("temporary.dll", new List<SyntaxTree> { tree }, metaDatas, compileOptions);



            var root = tree.GetCompilationUnitRoot();
            var stmt = SyntaxFactory.ParseStatement($"Metrics.Metrics.Increment(Guid.Parse(\"{guid}\"));");
            var stmt1 = SyntaxFactory.ParseStatement($"Metrics.Metrics.Decrement(Guid.Parse(\"{guid}\"));");
            var stmtCheck = SyntaxFactory.ParseExpression($"Metrics.Metrics.CheckExceeded(Guid.Parse(\"{guid}\"))");
            var rwr = new RewriterFactory().Add(new ReturnSeparateRewriter(compilation.GetSemanticModel(tree,true))).Add(new RewriterReturn(stmt1)).Add(new RewriterEnd(stmt1)).Add(new ConditionalReturnRewriter(stmtCheck)).Add(new RewriterStart(stmt));
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

        public int? GetRes(string _class,string _method,object[] args)
        {

            Guid sessionGuid = Guid.NewGuid();
            Metrics.Metrics.Init(sessionGuid,GlobalConstraints.MaxRecursionDepth);
            var tree = ConvertTree(this.tree, sessionGuid);
            
            var compileOptions = new CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary);
           

            List<MetadataReference> metaDatas = new List<MetadataReference>();
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var h in allAssemblies)
            {
                if (requestedAssemblies.Contains(h.GetName().Name))
                {
                    metaDatas.Add(MetadataReference.CreateFromFile(h.Location));
                }
            }


            var compilation = CSharpCompilation.Create("assembly.dll", new List<SyntaxTree> { tree }, metaDatas, compileOptions);
            
            MemoryStream ms = new MemoryStream();

            var ress = compilation.Emit(ms);
            Assembly asm = Assembly.Load(ms.GetBuffer());

            var ops = asm.GetType(_class);
            var obj = Activator.CreateInstance(ops);
            var meth = ops.GetMethod(_method);
            
            meth.Invoke(obj, args);


            bool exceeded = false;
            var count = Metrics.Metrics.GetLocMax(sessionGuid);
            exceeded = Metrics.Metrics.CheckExceeded(sessionGuid);
            Metrics.Metrics.Clear(sessionGuid);
            if (exceeded)
                return null;
            return count;
        }
        
    }
}