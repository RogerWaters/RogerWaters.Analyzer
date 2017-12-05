using System;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Z3;
using RogerWaters.Analyzer.Some;

namespace RogerWaters.Analyzer.NotNullAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class RogerWatersAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => DiagnosticDefinition.Descriptors.ToImmutableArray();

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            Contract.Requires(!false);
            try
            {
                using (Context ctx = new Context())
                {
                    var method = new Method(context.Node, context,ctx);
                    Solve(method);
                }
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create
                (
                    DiagnosticDefinition.Get("RA0003"),
                    context.Node.GetLocation(),
                    ex
                ));
            }
        }

        private static void Solve(Method context)
        {
            var solver = context.Z3.MkSimpleSolver();
            BoolExpr requireCondition;
            if (context.Requires.Take(2).Count() == 2)
            {
                requireCondition = context.Z3.MkAnd(context.Requires);
            }
            else if (context.Requires.Any())
            {
                requireCondition = context.Requires.First();
            }
            else
            {
                // no requires
                return;
            }
            requireCondition = requireCondition.Simplify() as BoolExpr;
            solver.Add(requireCondition);
            var status = solver.Check();
            if (status == Status.SATISFIABLE)
            {
                StringBuilder sb = new StringBuilder();
                foreach (var condition in context.Parameters.SelectMany(p => p.AllNamedExpressions))
                {
                    var val = solver.Model.Eval(condition.Value);
                    sb.Append(condition.Key);
                    sb.Append(" => ");
                    sb.AppendLine(val.ToString());
                }
                
                foreach (var location in context.RequireConditions.Select(c => c.GetLocation()))
                {
                    context.NodeAnalysisContext.ReportDiagnostic(Diagnostic.Create
                    (
                        DiagnosticDefinition.Get("RA0000"),
                        location,
                        sb.ToString()
                    ));
                }
                return;
            }
            if (status == Status.UNSATISFIABLE)
            {
                StringBuilder sb = new StringBuilder();
                foreach (BoolExpr c in solver.UnsatCore)
                {
                    sb.AppendFormat("{0}", c);
                    sb.AppendLine();
                }
                foreach (var location in context.RequireConditions.Select(c => c.GetLocation()))
                {
                    context.NodeAnalysisContext.ReportDiagnostic(Diagnostic.Create
                    (
                        DiagnosticDefinition.Get("RA0001"),
                        location,
                        requireCondition
                    ));
                }
                return;
            }
            if (status == Status.UNKNOWN)
            {
                foreach (var location in context.RequireConditions.Select(c => c.GetLocation()))
                {
                    context.NodeAnalysisContext.ReportDiagnostic(Diagnostic.Create
                    (
                        DiagnosticDefinition.Get("RA0002"),
                        location,
                        requireCondition
                    ));
                }
                return;
            }
        }
    }
}