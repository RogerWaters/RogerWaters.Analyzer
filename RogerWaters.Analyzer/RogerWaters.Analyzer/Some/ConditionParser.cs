using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Z3;

namespace RogerWaters.Analyzer.Some
{
    internal class ConditionParser : CSharpSyntaxVisitor<Expr>
    {
        private readonly Method _method;
        private readonly SemanticModel _semanticModel;
        private bool _nullCompare;
        private List<BoolExpr> _cachedExprs = new List<BoolExpr>();
        

        public ConditionParser(Method method,SemanticModel semanticModel)
        {
            _method = method;
            _semanticModel = semanticModel;
        }

        public override Expr Visit(SyntaxNode node)
        {
            return base.Visit(node);
        }

        public override Expr DefaultVisit(SyntaxNode node)
        {
            if (node.SyntaxTree.TryGetText(out var text))
            {
                throw new NotImplementedException(text.ToString(node.Span));
            }
            throw new NotImplementedException();
        }

        public override Expr VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (_semanticModel.GetSymbolInfo(node).Symbol is IParameterSymbol parameter)
            {
                if (_nullCompare)
                {
                    return _method[parameter].Null;
                }
                return _method[parameter].Value;
            }

            return base.VisitIdentifierName(node);
        }

        public override Expr VisitQualifiedName(QualifiedNameSyntax node)
        {
            return base.VisitQualifiedName(node);
        }

        public override Expr VisitGenericName(GenericNameSyntax node)
        {
            return base.VisitGenericName(node);
        }

        public override Expr VisitTypeArgumentList(TypeArgumentListSyntax node)
        {
            return base.VisitTypeArgumentList(node);
        }

        public override Expr VisitAliasQualifiedName(AliasQualifiedNameSyntax node)
        {
            return base.VisitAliasQualifiedName(node);
        }

        public override Expr VisitPredefinedType(PredefinedTypeSyntax node)
        {
            return base.VisitPredefinedType(node);
        }

        public override Expr VisitArrayType(ArrayTypeSyntax node)
        {
            return base.VisitArrayType(node);
        }

        public override Expr VisitArrayRankSpecifier(ArrayRankSpecifierSyntax node)
        {
            return base.VisitArrayRankSpecifier(node);
        }

        public override Expr VisitPointerType(PointerTypeSyntax node)
        {
            return base.VisitPointerType(node);
        }

        public override Expr VisitNullableType(NullableTypeSyntax node)
        {
            return base.VisitNullableType(node);
        }

        public override Expr VisitOmittedTypeArgument(OmittedTypeArgumentSyntax node)
        {
            return base.VisitOmittedTypeArgument(node);
        }

        public override Expr VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
        {
            return base.VisitParenthesizedExpression(node);
        }

        public override Expr VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
        {
            return base.VisitPrefixUnaryExpression(node);
        }

        public override Expr VisitAwaitExpression(AwaitExpressionSyntax node)
        {
            return base.VisitAwaitExpression(node);
        }

        public override Expr VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
        {
            return base.VisitPostfixUnaryExpression(node);
        }

        public override Expr VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var memberSymbol = _semanticModel.GetSymbolInfo(node).Symbol as IPropertySymbol;

            if (node.Expression is IdentifierNameSyntax && memberSymbol != null)
            {
                var parameter = _semanticModel.GetSymbolInfo(node.Expression).Symbol as IParameterSymbol;
                if (!memberSymbol.IsIndexer)
                {
                    return _method[parameter].GetOrAdd(memberSymbol.Name, name =>
                    {
                        switch (memberSymbol.Type.SpecialType)
                        {
                            case SpecialType.System_Byte:
                            case SpecialType.System_SByte:
                            case SpecialType.System_Int16:
                            case SpecialType.System_Int32:
                            case SpecialType.System_Int64:
                                return _method.Z3.MkIntConst(name);
                            case SpecialType.System_String:
                                return _method.Z3.MkConst(name, _method.Z3.StringSort);
                            default:
                                throw new ArgumentOutOfRangeException(nameof(node),
                                    memberSymbol.ContainingType.SpecialType, "Not supported Type");
                        }
                    });
                }
            }
            return DefaultVisit(node);
        }

        public override Expr VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
        {
            return base.VisitConditionalAccessExpression(node);
        }

        public override Expr VisitMemberBindingExpression(MemberBindingExpressionSyntax node)
        {
            return base.VisitMemberBindingExpression(node);
        }

        public override Expr VisitElementBindingExpression(ElementBindingExpressionSyntax node)
        {
            return base.VisitElementBindingExpression(node);
        }

        public override Expr VisitImplicitElementAccess(ImplicitElementAccessSyntax node)
        {
            return base.VisitImplicitElementAccess(node);
        }

        public override Expr VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            if (NullEval(node) is Expr e)
            {
                return e;
            }
            var tmp = _cachedExprs;
            _cachedExprs = new List<BoolExpr>();
            var left = Visit(node.Left);
            var leftConditions = _cachedExprs;
            _cachedExprs = new List<BoolExpr>();
            var right = Visit(node.Right);
            var rightConditions = _cachedExprs;
            _cachedExprs = tmp;
            switch (node.Kind())
            {
                case SyntaxKind.EqualsExpression:
                    return Combine(_method.Z3.MkEq(left, right),leftConditions.Union(rightConditions).ToList());
                case SyntaxKind.NotEqualsExpression:
                    return Combine(_method.Z3.MkNot(_method.Z3.MkEq(left, right)), leftConditions.Union(rightConditions).ToList());
                case SyntaxKind.GreaterThanExpression:
                    return Combine(_method.Z3.MkGt(left as ArithExpr, right as ArithExpr), leftConditions.Union(rightConditions).ToList());
                case SyntaxKind.GreaterThanOrEqualExpression:
                    return Combine(_method.Z3.MkGe(left as ArithExpr, right as ArithExpr), leftConditions.Union(rightConditions).ToList());
                case SyntaxKind.LessThanExpression:
                    return Combine(_method.Z3.MkLt(left as ArithExpr, right as ArithExpr), leftConditions.Union(rightConditions).ToList());
                case SyntaxKind.LessThanOrEqualExpression:
                    return Combine(_method.Z3.MkLe(left as ArithExpr, right as ArithExpr), leftConditions.Union(rightConditions).ToList());
                case SyntaxKind.LogicalAndExpression:
                    return _method.Z3.MkAnd(Combine(left as BoolExpr, leftConditions), Combine(right as BoolExpr, rightConditions));
                case SyntaxKind.LogicalOrExpression:
                    return _method.Z3.MkOr(Combine(left as BoolExpr, leftConditions), Combine(right as BoolExpr, rightConditions));
                default:
                    throw new ArgumentOutOfRangeException(nameof(node),node.Kind(),"Not supported");
            }
        }

        private BoolExpr Combine(BoolExpr expr, List<BoolExpr> conditions)
        {
            if (conditions.Count > 0)
            {
                conditions.Add(expr);
                return _method.Z3.MkAnd(conditions.ToArray());
            }
            return expr;
        }

        private Expr NullEval(BinaryExpressionSyntax node)
        {
            try
            {
                _nullCompare = true;
                if (node.IsKind(SyntaxKind.EqualsExpression))
                {
                    if (node.Left is LiteralExpressionSyntax literal && literal.Token.Value == null)
                    {
                        return _method.Z3.MkEq(_method.Z3.MkTrue(), Visit(node.Right));
                    }
                    else if (node.Right is LiteralExpressionSyntax literalR && literalR.Token.Value == null)
                    {
                        return _method.Z3.MkEq(_method.Z3.MkTrue(), Visit(node.Left));
                    }
                }
                if (node.IsKind(SyntaxKind.NotEqualsExpression))
                {
                    if (node.Left is LiteralExpressionSyntax literal && literal.Token.Value == null)
                    {
                        return _method.Z3.MkEq(_method.Z3.MkFalse(), Visit(node.Right));
                    }
                    else if (node.Right is LiteralExpressionSyntax literalR && literalR.Token.Value == null)
                    {
                        return _method.Z3.MkEq(_method.Z3.MkFalse(), Visit(node.Left));
                    }
                }
                return null;
            }
            finally
            {
                _nullCompare = false;
            }
        }

        public override Expr VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            return base.VisitAssignmentExpression(node);
        }

        public override Expr VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            return base.VisitConditionalExpression(node);
        }

        public override Expr VisitThisExpression(ThisExpressionSyntax node)
        {
            return base.VisitThisExpression(node);
        }

        public override Expr VisitBaseExpression(BaseExpressionSyntax node)
        {
            return base.VisitBaseExpression(node);
        }

        public override Expr VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            var value = node.Token.Value;

            if (value is ValueType v)
            {
                if (v is bool b)
                {
                    return _method.Z3.MkBool(b);
                }
                return _method.Z3.MkInt(v.ToString());
            }
            return _method.Z3.MkString(value as String);
        }

        public override Expr VisitMakeRefExpression(MakeRefExpressionSyntax node)
        {
            return base.VisitMakeRefExpression(node);
        }

        public override Expr VisitRefTypeExpression(RefTypeExpressionSyntax node)
        {
            return base.VisitRefTypeExpression(node);
        }

        public override Expr VisitRefValueExpression(RefValueExpressionSyntax node)
        {
            return base.VisitRefValueExpression(node);
        }

        public override Expr VisitCheckedExpression(CheckedExpressionSyntax node)
        {
            return base.VisitCheckedExpression(node);
        }

        public override Expr VisitDefaultExpression(DefaultExpressionSyntax node)
        {
            return base.VisitDefaultExpression(node);
        }

        public override Expr VisitTypeOfExpression(TypeOfExpressionSyntax node)
        {
            return base.VisitTypeOfExpression(node);
        }

        public override Expr VisitSizeOfExpression(SizeOfExpressionSyntax node)
        {
            return base.VisitSizeOfExpression(node);
        }

        public override Expr VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            return base.VisitInvocationExpression(node);
        }

        public override Expr VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            if (node.Expression is IdentifierNameSyntax identifier && _method.TryGetParameter(identifier, out var parameter))
            {
                if (node.ArgumentList.Arguments.Count == 1 && parameter.IsArray)
                {
                    var arg = this.Visit(node.ArgumentList.Arguments[0].Expression);
                    
                    _cachedExprs.Add(_method.Z3.MkAnd
                    (
                        //NotNull
                        _method.Z3.MkEq(parameter.Null, _method.Z3.MkFalse()),
                        //Count > arg
                        _method.Z3.MkGt(parameter.GetOrAdd("Length", name => _method.Z3.MkIntConst(name)) as ArithExpr, arg as ArithExpr)
                    ));

                    return parameter.GetOrAdd($"Value[{arg}]", name => _method.Z3.MkSelect(parameter.Value as ArrayExpr, arg));
                }
            }
            return base.VisitElementAccessExpression(node);
        }

        public override Expr VisitArgumentList(ArgumentListSyntax node)
        {
            return base.VisitArgumentList(node);
        }

        public override Expr VisitBracketedArgumentList(BracketedArgumentListSyntax node)
        {
            return base.VisitBracketedArgumentList(node);
        }

        public override Expr VisitArgument(ArgumentSyntax node)
        {
            return base.VisitArgument(node);
        }

        public override Expr VisitNameColon(NameColonSyntax node)
        {
            return base.VisitNameColon(node);
        }

        public override Expr VisitCastExpression(CastExpressionSyntax node)
        {
            return base.VisitCastExpression(node);
        }

        public override Expr VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
        {
            return base.VisitAnonymousMethodExpression(node);
        }

        public override Expr VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
        {
            return base.VisitSimpleLambdaExpression(node);
        }

        public override Expr VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
        {
            return base.VisitParenthesizedLambdaExpression(node);
        }

        public override Expr VisitInitializerExpression(InitializerExpressionSyntax node)
        {
            return base.VisitInitializerExpression(node);
        }

        public override Expr VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            return base.VisitObjectCreationExpression(node);
        }

        public override Expr VisitAnonymousObjectMemberDeclarator(AnonymousObjectMemberDeclaratorSyntax node)
        {
            return base.VisitAnonymousObjectMemberDeclarator(node);
        }

        public override Expr VisitAnonymousObjectCreationExpression(AnonymousObjectCreationExpressionSyntax node)
        {
            return base.VisitAnonymousObjectCreationExpression(node);
        }

        public override Expr VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
        {
            return base.VisitArrayCreationExpression(node);
        }

        public override Expr VisitImplicitArrayCreationExpression(ImplicitArrayCreationExpressionSyntax node)
        {
            return base.VisitImplicitArrayCreationExpression(node);
        }

        public override Expr VisitStackAllocArrayCreationExpression(StackAllocArrayCreationExpressionSyntax node)
        {
            return base.VisitStackAllocArrayCreationExpression(node);
        }

        public override Expr VisitQueryExpression(QueryExpressionSyntax node)
        {
            return base.VisitQueryExpression(node);
        }

        public override Expr VisitQueryBody(QueryBodySyntax node)
        {
            return base.VisitQueryBody(node);
        }

        public override Expr VisitFromClause(FromClauseSyntax node)
        {
            return base.VisitFromClause(node);
        }

        public override Expr VisitLetClause(LetClauseSyntax node)
        {
            return base.VisitLetClause(node);
        }

        public override Expr VisitJoinClause(JoinClauseSyntax node)
        {
            return base.VisitJoinClause(node);
        }

        public override Expr VisitJoinIntoClause(JoinIntoClauseSyntax node)
        {
            return base.VisitJoinIntoClause(node);
        }

        public override Expr VisitWhereClause(WhereClauseSyntax node)
        {
            return base.VisitWhereClause(node);
        }

        public override Expr VisitOrderByClause(OrderByClauseSyntax node)
        {
            return base.VisitOrderByClause(node);
        }

        public override Expr VisitOrdering(OrderingSyntax node)
        {
            return base.VisitOrdering(node);
        }

        public override Expr VisitSelectClause(SelectClauseSyntax node)
        {
            return base.VisitSelectClause(node);
        }

        public override Expr VisitGroupClause(GroupClauseSyntax node)
        {
            return base.VisitGroupClause(node);
        }

        public override Expr VisitQueryContinuation(QueryContinuationSyntax node)
        {
            return base.VisitQueryContinuation(node);
        }

        public override Expr VisitOmittedArraySizeExpression(OmittedArraySizeExpressionSyntax node)
        {
            return base.VisitOmittedArraySizeExpression(node);
        }

        public override Expr VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
        {
            return base.VisitInterpolatedStringExpression(node);
        }

        public override Expr VisitInterpolatedStringText(InterpolatedStringTextSyntax node)
        {
            return base.VisitInterpolatedStringText(node);
        }

        public override Expr VisitInterpolation(InterpolationSyntax node)
        {
            return base.VisitInterpolation(node);
        }

        public override Expr VisitInterpolationAlignmentClause(InterpolationAlignmentClauseSyntax node)
        {
            return base.VisitInterpolationAlignmentClause(node);
        }

        public override Expr VisitInterpolationFormatClause(InterpolationFormatClauseSyntax node)
        {
            return base.VisitInterpolationFormatClause(node);
        }

        public override Expr VisitGlobalStatement(GlobalStatementSyntax node)
        {
            return base.VisitGlobalStatement(node);
        }

        public override Expr VisitBlock(BlockSyntax node)
        {
            return base.VisitBlock(node);
        }

        public override Expr VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            return base.VisitLocalDeclarationStatement(node);
        }

        public override Expr VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            return base.VisitVariableDeclaration(node);
        }

        public override Expr VisitVariableDeclarator(VariableDeclaratorSyntax node)
        {
            return base.VisitVariableDeclarator(node);
        }

        public override Expr VisitEqualsValueClause(EqualsValueClauseSyntax node)
        {
            return base.VisitEqualsValueClause(node);
        }

        public override Expr VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            return base.VisitExpressionStatement(node);
        }

        public override Expr VisitEmptyStatement(EmptyStatementSyntax node)
        {
            return base.VisitEmptyStatement(node);
        }

        public override Expr VisitLabeledStatement(LabeledStatementSyntax node)
        {
            return base.VisitLabeledStatement(node);
        }

        public override Expr VisitGotoStatement(GotoStatementSyntax node)
        {
            return base.VisitGotoStatement(node);
        }

        public override Expr VisitBreakStatement(BreakStatementSyntax node)
        {
            return base.VisitBreakStatement(node);
        }

        public override Expr VisitContinueStatement(ContinueStatementSyntax node)
        {
            return base.VisitContinueStatement(node);
        }

        public override Expr VisitReturnStatement(ReturnStatementSyntax node)
        {
            return base.VisitReturnStatement(node);
        }

        public override Expr VisitThrowStatement(ThrowStatementSyntax node)
        {
            return base.VisitThrowStatement(node);
        }

        public override Expr VisitYieldStatement(YieldStatementSyntax node)
        {
            return base.VisitYieldStatement(node);
        }

        public override Expr VisitWhileStatement(WhileStatementSyntax node)
        {
            return base.VisitWhileStatement(node);
        }

        public override Expr VisitDoStatement(DoStatementSyntax node)
        {
            return base.VisitDoStatement(node);
        }

        public override Expr VisitForStatement(ForStatementSyntax node)
        {
            return base.VisitForStatement(node);
        }

        public override Expr VisitForEachStatement(ForEachStatementSyntax node)
        {
            return base.VisitForEachStatement(node);
        }

        public override Expr VisitUsingStatement(UsingStatementSyntax node)
        {
            return base.VisitUsingStatement(node);
        }

        public override Expr VisitFixedStatement(FixedStatementSyntax node)
        {
            return base.VisitFixedStatement(node);
        }

        public override Expr VisitCheckedStatement(CheckedStatementSyntax node)
        {
            return base.VisitCheckedStatement(node);
        }

        public override Expr VisitUnsafeStatement(UnsafeStatementSyntax node)
        {
            return base.VisitUnsafeStatement(node);
        }

        public override Expr VisitLockStatement(LockStatementSyntax node)
        {
            return base.VisitLockStatement(node);
        }

        public override Expr VisitIfStatement(IfStatementSyntax node)
        {
            return base.VisitIfStatement(node);
        }

        public override Expr VisitElseClause(ElseClauseSyntax node)
        {
            return base.VisitElseClause(node);
        }

        public override Expr VisitSwitchStatement(SwitchStatementSyntax node)
        {
            return base.VisitSwitchStatement(node);
        }

        public override Expr VisitSwitchSection(SwitchSectionSyntax node)
        {
            return base.VisitSwitchSection(node);
        }

        public override Expr VisitCaseSwitchLabel(CaseSwitchLabelSyntax node)
        {
            return base.VisitCaseSwitchLabel(node);
        }

        public override Expr VisitDefaultSwitchLabel(DefaultSwitchLabelSyntax node)
        {
            return base.VisitDefaultSwitchLabel(node);
        }

        public override Expr VisitTryStatement(TryStatementSyntax node)
        {
            return base.VisitTryStatement(node);
        }

        public override Expr VisitCatchClause(CatchClauseSyntax node)
        {
            return base.VisitCatchClause(node);
        }

        public override Expr VisitCatchDeclaration(CatchDeclarationSyntax node)
        {
            return base.VisitCatchDeclaration(node);
        }

        public override Expr VisitCatchFilterClause(CatchFilterClauseSyntax node)
        {
            return base.VisitCatchFilterClause(node);
        }

        public override Expr VisitFinallyClause(FinallyClauseSyntax node)
        {
            return base.VisitFinallyClause(node);
        }

        public override Expr VisitCompilationUnit(CompilationUnitSyntax node)
        {
            return base.VisitCompilationUnit(node);
        }

        public override Expr VisitExternAliasDirective(ExternAliasDirectiveSyntax node)
        {
            return base.VisitExternAliasDirective(node);
        }

        public override Expr VisitUsingDirective(UsingDirectiveSyntax node)
        {
            return base.VisitUsingDirective(node);
        }

        public override Expr VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            return base.VisitNamespaceDeclaration(node);
        }

        public override Expr VisitAttributeList(AttributeListSyntax node)
        {
            return base.VisitAttributeList(node);
        }

        public override Expr VisitAttributeTargetSpecifier(AttributeTargetSpecifierSyntax node)
        {
            return base.VisitAttributeTargetSpecifier(node);
        }

        public override Expr VisitAttribute(AttributeSyntax node)
        {
            return base.VisitAttribute(node);
        }

        public override Expr VisitAttributeArgumentList(AttributeArgumentListSyntax node)
        {
            return base.VisitAttributeArgumentList(node);
        }

        public override Expr VisitAttributeArgument(AttributeArgumentSyntax node)
        {
            return base.VisitAttributeArgument(node);
        }

        public override Expr VisitNameEquals(NameEqualsSyntax node)
        {
            return base.VisitNameEquals(node);
        }

        public override Expr VisitTypeParameterList(TypeParameterListSyntax node)
        {
            return base.VisitTypeParameterList(node);
        }

        public override Expr VisitTypeParameter(TypeParameterSyntax node)
        {
            return base.VisitTypeParameter(node);
        }

        public override Expr VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            return base.VisitClassDeclaration(node);
        }

        public override Expr VisitStructDeclaration(StructDeclarationSyntax node)
        {
            return base.VisitStructDeclaration(node);
        }

        public override Expr VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            return base.VisitInterfaceDeclaration(node);
        }

        public override Expr VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            return base.VisitEnumDeclaration(node);
        }

        public override Expr VisitDelegateDeclaration(DelegateDeclarationSyntax node)
        {
            return base.VisitDelegateDeclaration(node);
        }

        public override Expr VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
        {
            return base.VisitEnumMemberDeclaration(node);
        }

        public override Expr VisitBaseList(BaseListSyntax node)
        {
            return base.VisitBaseList(node);
        }

        public override Expr VisitSimpleBaseType(SimpleBaseTypeSyntax node)
        {
            return base.VisitSimpleBaseType(node);
        }

        public override Expr VisitTypeParameterConstraintClause(TypeParameterConstraintClauseSyntax node)
        {
            return base.VisitTypeParameterConstraintClause(node);
        }

        public override Expr VisitConstructorConstraint(ConstructorConstraintSyntax node)
        {
            return base.VisitConstructorConstraint(node);
        }

        public override Expr VisitClassOrStructConstraint(ClassOrStructConstraintSyntax node)
        {
            return base.VisitClassOrStructConstraint(node);
        }

        public override Expr VisitTypeConstraint(TypeConstraintSyntax node)
        {
            return base.VisitTypeConstraint(node);
        }

        public override Expr VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            return base.VisitFieldDeclaration(node);
        }

        public override Expr VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
        {
            return base.VisitEventFieldDeclaration(node);
        }

        public override Expr VisitExplicitInterfaceSpecifier(ExplicitInterfaceSpecifierSyntax node)
        {
            return base.VisitExplicitInterfaceSpecifier(node);
        }

        public override Expr VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            return base.VisitMethodDeclaration(node);
        }

        public override Expr VisitOperatorDeclaration(OperatorDeclarationSyntax node)
        {
            return base.VisitOperatorDeclaration(node);
        }

        public override Expr VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
        {
            return base.VisitConversionOperatorDeclaration(node);
        }

        public override Expr VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            return base.VisitConstructorDeclaration(node);
        }

        public override Expr VisitConstructorInitializer(ConstructorInitializerSyntax node)
        {
            return base.VisitConstructorInitializer(node);
        }

        public override Expr VisitDestructorDeclaration(DestructorDeclarationSyntax node)
        {
            return base.VisitDestructorDeclaration(node);
        }

        public override Expr VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            return base.VisitPropertyDeclaration(node);
        }

        public override Expr VisitArrowExpressionClause(ArrowExpressionClauseSyntax node)
        {
            return base.VisitArrowExpressionClause(node);
        }

        public override Expr VisitEventDeclaration(EventDeclarationSyntax node)
        {
            return base.VisitEventDeclaration(node);
        }

        public override Expr VisitIndexerDeclaration(IndexerDeclarationSyntax node)
        {
            return base.VisitIndexerDeclaration(node);
        }

        public override Expr VisitAccessorList(AccessorListSyntax node)
        {
            return base.VisitAccessorList(node);
        }

        public override Expr VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            return base.VisitAccessorDeclaration(node);
        }

        public override Expr VisitParameterList(ParameterListSyntax node)
        {
            return base.VisitParameterList(node);
        }

        public override Expr VisitBracketedParameterList(BracketedParameterListSyntax node)
        {
            return base.VisitBracketedParameterList(node);
        }

        public override Expr VisitParameter(ParameterSyntax node)
        {
            return base.VisitParameter(node);
        }

        public override Expr VisitIncompleteMember(IncompleteMemberSyntax node)
        {
            return base.VisitIncompleteMember(node);
        }

        public override Expr VisitSkippedTokensTrivia(SkippedTokensTriviaSyntax node)
        {
            return base.VisitSkippedTokensTrivia(node);
        }

        public override Expr VisitDocumentationCommentTrivia(DocumentationCommentTriviaSyntax node)
        {
            return base.VisitDocumentationCommentTrivia(node);
        }

        public override Expr VisitTypeCref(TypeCrefSyntax node)
        {
            return base.VisitTypeCref(node);
        }

        public override Expr VisitQualifiedCref(QualifiedCrefSyntax node)
        {
            return base.VisitQualifiedCref(node);
        }

        public override Expr VisitNameMemberCref(NameMemberCrefSyntax node)
        {
            return base.VisitNameMemberCref(node);
        }

        public override Expr VisitIndexerMemberCref(IndexerMemberCrefSyntax node)
        {
            return base.VisitIndexerMemberCref(node);
        }

        public override Expr VisitOperatorMemberCref(OperatorMemberCrefSyntax node)
        {
            return base.VisitOperatorMemberCref(node);
        }

        public override Expr VisitConversionOperatorMemberCref(ConversionOperatorMemberCrefSyntax node)
        {
            return base.VisitConversionOperatorMemberCref(node);
        }

        public override Expr VisitCrefParameterList(CrefParameterListSyntax node)
        {
            return base.VisitCrefParameterList(node);
        }

        public override Expr VisitCrefBracketedParameterList(CrefBracketedParameterListSyntax node)
        {
            return base.VisitCrefBracketedParameterList(node);
        }

        public override Expr VisitCrefParameter(CrefParameterSyntax node)
        {
            return base.VisitCrefParameter(node);
        }

        public override Expr VisitXmlElement(XmlElementSyntax node)
        {
            return base.VisitXmlElement(node);
        }

        public override Expr VisitXmlElementStartTag(XmlElementStartTagSyntax node)
        {
            return base.VisitXmlElementStartTag(node);
        }

        public override Expr VisitXmlElementEndTag(XmlElementEndTagSyntax node)
        {
            return base.VisitXmlElementEndTag(node);
        }

        public override Expr VisitXmlEmptyElement(XmlEmptyElementSyntax node)
        {
            return base.VisitXmlEmptyElement(node);
        }

        public override Expr VisitXmlName(XmlNameSyntax node)
        {
            return base.VisitXmlName(node);
        }

        public override Expr VisitXmlPrefix(XmlPrefixSyntax node)
        {
            return base.VisitXmlPrefix(node);
        }

        public override Expr VisitXmlTextAttribute(XmlTextAttributeSyntax node)
        {
            return base.VisitXmlTextAttribute(node);
        }

        public override Expr VisitXmlCrefAttribute(XmlCrefAttributeSyntax node)
        {
            return base.VisitXmlCrefAttribute(node);
        }

        public override Expr VisitXmlNameAttribute(XmlNameAttributeSyntax node)
        {
            return base.VisitXmlNameAttribute(node);
        }

        public override Expr VisitXmlText(XmlTextSyntax node)
        {
            return base.VisitXmlText(node);
        }

        public override Expr VisitXmlCDataSection(XmlCDataSectionSyntax node)
        {
            return base.VisitXmlCDataSection(node);
        }

        public override Expr VisitXmlProcessingInstruction(XmlProcessingInstructionSyntax node)
        {
            return base.VisitXmlProcessingInstruction(node);
        }

        public override Expr VisitXmlComment(XmlCommentSyntax node)
        {
            return base.VisitXmlComment(node);
        }

        public override Expr VisitIfDirectiveTrivia(IfDirectiveTriviaSyntax node)
        {
            return base.VisitIfDirectiveTrivia(node);
        }

        public override Expr VisitElifDirectiveTrivia(ElifDirectiveTriviaSyntax node)
        {
            return base.VisitElifDirectiveTrivia(node);
        }

        public override Expr VisitElseDirectiveTrivia(ElseDirectiveTriviaSyntax node)
        {
            return base.VisitElseDirectiveTrivia(node);
        }

        public override Expr VisitEndIfDirectiveTrivia(EndIfDirectiveTriviaSyntax node)
        {
            return base.VisitEndIfDirectiveTrivia(node);
        }

        public override Expr VisitRegionDirectiveTrivia(RegionDirectiveTriviaSyntax node)
        {
            return base.VisitRegionDirectiveTrivia(node);
        }

        public override Expr VisitEndRegionDirectiveTrivia(EndRegionDirectiveTriviaSyntax node)
        {
            return base.VisitEndRegionDirectiveTrivia(node);
        }

        public override Expr VisitErrorDirectiveTrivia(ErrorDirectiveTriviaSyntax node)
        {
            return base.VisitErrorDirectiveTrivia(node);
        }

        public override Expr VisitWarningDirectiveTrivia(WarningDirectiveTriviaSyntax node)
        {
            return base.VisitWarningDirectiveTrivia(node);
        }

        public override Expr VisitBadDirectiveTrivia(BadDirectiveTriviaSyntax node)
        {
            return base.VisitBadDirectiveTrivia(node);
        }

        public override Expr VisitDefineDirectiveTrivia(DefineDirectiveTriviaSyntax node)
        {
            return base.VisitDefineDirectiveTrivia(node);
        }

        public override Expr VisitUndefDirectiveTrivia(UndefDirectiveTriviaSyntax node)
        {
            return base.VisitUndefDirectiveTrivia(node);
        }

        public override Expr VisitLineDirectiveTrivia(LineDirectiveTriviaSyntax node)
        {
            return base.VisitLineDirectiveTrivia(node);
        }

        public override Expr VisitPragmaWarningDirectiveTrivia(PragmaWarningDirectiveTriviaSyntax node)
        {
            return base.VisitPragmaWarningDirectiveTrivia(node);
        }

        public override Expr VisitPragmaChecksumDirectiveTrivia(PragmaChecksumDirectiveTriviaSyntax node)
        {
            return base.VisitPragmaChecksumDirectiveTrivia(node);
        }

        public override Expr VisitReferenceDirectiveTrivia(ReferenceDirectiveTriviaSyntax node)
        {
            return base.VisitReferenceDirectiveTrivia(node);
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }
}