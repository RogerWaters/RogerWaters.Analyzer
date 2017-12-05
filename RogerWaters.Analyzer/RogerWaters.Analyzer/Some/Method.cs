using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Z3;

namespace RogerWaters.Analyzer.Some
{
    internal sealed class Method
    {
        private readonly IMethodSymbol _method;
        private readonly MethodDeclarationSyntax _methodNode;
        public SyntaxNodeAnalysisContext NodeAnalysisContext { get; }
        private readonly Context _ctx;

        private List<ExpressionSyntax> _requireConditions = null;
        private List<Parameter> _parameters;
        private readonly List<BoolExpr> _requires = new List<BoolExpr>();
        private readonly List<Expr> _additional = new List<Expr>();


        public Method(SyntaxNode methodNode, SyntaxNodeAnalysisContext syntaxNodeAnalysisContext, Context ctx)
        {
            _methodNode = methodNode as MethodDeclarationSyntax;
            _method = syntaxNodeAnalysisContext.SemanticModel.GetDeclaredSymbol(_methodNode) as IMethodSymbol;
            NodeAnalysisContext = syntaxNodeAnalysisContext;
            _ctx = ctx;
            ParseRequires();
        }

        private void ParseRequires()
        {
            foreach (var condition in RequireConditions)
            {
                var walker = new ConditionParser(this, NodeAnalysisContext.SemanticModel);
                var expr = condition.Accept(walker);
                _requires.Add(expr as BoolExpr);
            }
        }

        public Context Z3 => _ctx;

        public IEnumerable<BoolExpr> Requires => _requires;

        public IEnumerable<Expr> AdditionalExpressions => _parameters.Where(p => p.HasNull).Select(p => p.Null);
        public IEnumerable<Expr> ParameterValueExprs => _parameters.Where(p => p.HasValue).Select(p => p.Value);

        public SemanticModel GetSemanticModel => NodeAnalysisContext.SemanticModel;

        public IEnumerable<Parameter> Parameters
        {
            get
            {
                if (_parameters == null)
                {
                    _parameters = _method.Parameters
                        .Select(p => new Parameter(p,_ctx))
                        .ToList();
                }
                return _parameters;
            }
        }

        public IEnumerable<ExpressionSyntax> RequireConditions
        {
            get
            {
                if (_requireConditions == null)
                {
                    _requireConditions = _methodNode
                        .Body
                        .Statements
                        .OfType<ExpressionStatementSyntax>()
                        .Select(e => e.Expression)
                        .OfType<InvocationExpressionSyntax>()
                        .Where(IsContractRequires)
                        .Select(m => m.ArgumentList.Arguments[0].Expression)
                        .ToList();
                }
                return _requireConditions;
            }
        }

        private bool IsContractRequires(InvocationExpressionSyntax invocation)
        {
            return 
                NodeAnalysisContext.SemanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol method && 
                method.Name == nameof(Contract.Requires) && 
                IsCodeContractMethod(method);
        }

        private static bool IsCodeContractMethod(IMethodSymbol method)
        {
            if (method == null || method.ContainingType?.Name != nameof(Contract))
                return false;

            var nsSymbol = method.ContainingType.ContainingNamespace;

            for (var i = 0; i < 3; i++)
            {
                if (nsSymbol == null)
                    return false;
                switch (i)
                {
                    case 0:
                        if (nsSymbol.Name != nameof(System.Diagnostics.Contracts))
                            return false;
                        break;
                    case 1:
                        if (nsSymbol.Name != nameof(System.Diagnostics))
                            return false;
                        break;
                    case 2:
                        if (nsSymbol.Name != nameof(System))
                            return false;
                        break;
                }
                nsSymbol = nsSymbol.ContainingNamespace;
            }
            return nsSymbol.ContainingNamespace == null;
        }

        public Parameter this[IParameterSymbol symbol]
        {
            get
            {
                return Parameters.First(p => p.Name == symbol.Name);
            }
        }

        public bool TryGetParameter(IdentifierNameSyntax identifier, out Parameter parameter)
        {
            if (NodeAnalysisContext.SemanticModel.GetSymbolInfo(identifier).Symbol is IParameterSymbol p)
            {
                parameter = this[p];
                return true;
            }
            parameter = null;
            return false;
        }
    }
}
