using System;
using System.Linq;
using System.Linq.Expressions;
using JetBrains.Application.Settings;
using JetBrains.Application.Settings.Calculated.Interface;
using JetBrains.Application.Threading;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Plugins.FSharp.Psi.Impl.Tree;
using JetBrains.ReSharper.Plugins.FSharp.Psi.Parsing;
using JetBrains.ReSharper.Plugins.FSharp.Psi.Tree;
using JetBrains.ReSharper.Plugins.FSharp.Services.Formatter;
using JetBrains.ReSharper.Plugins.FSharp.Util;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CodeStyle;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Impl.CodeStyle;
using JetBrains.ReSharper.Psi.Tree;

namespace JetBrains.ReSharper.Plugins.FSharp.Psi.CodeFormatter
{
  [Language(typeof(FSharpLanguage))]
  public class FSharpFormatterInfoProvider :
    FormatterInfoProviderWithFluentApi<CodeFormattingContext, FSharpFormatSettingsKey>
  {
    public FSharpFormatterInfoProvider(ISettingsSchema settingsSchema,
      ICalculatedSettingsSchema calculatedSettingsSchema, IThreading threading, Lifetime lifetime)
      : base(settingsSchema, calculatedSettingsSchema, threading, lifetime)
    {
    }

    protected readonly NodeTypeSet AccessModifiers =
      new NodeTypeSet(FSharpTokenType.PUBLIC, FSharpTokenType.INTERNAL, FSharpTokenType.PRIVATE);

    protected override void Initialize()
    {
      base.Initialize();

      Indenting();
      Aligning();
      Formatting();
    }

    public override ProjectFileType MainProjectFileType => FSharpProjectFileType.Instance;

    private void Indenting()
    {
      var bindingAndModuleDeclIndentingRulesParameters = new[]
      {
        ("NestedModuleDeclaration", ElementType.NESTED_MODULE_DECLARATION, NestedModuleDeclaration.MODULE_MEMBER),
        ("TopBinding", ElementType.TOP_BINDING, TopBinding.CHAMELEON_EXPR),
        ("LocalBinding", ElementType.LOCAL_BINDING, LocalBinding.EXPR),
        ("NestedModuleDeclName", ElementType.NESTED_MODULE_DECLARATION, NestedModuleDeclaration.IDENTIFIER),
        ("NamedModuleDeclName", ElementType.NAMED_MODULE_DECLARATION, NamedModuleDeclaration.IDENTIFIER),
      };

      var fsExprIndentingRulesParameters = new[]
      {
        ("ForExpr", ElementType.FOR_EXPR, ForExpr.DO_EXPR),
        ("ForEachExpr", ElementType.FOR_EACH_EXPR, ForEachExpr.DO_EXPR),
        ("WhileExpr", ElementType.WHILE_EXPR, WhileExpr.DO_EXPR),
        ("DoExpr", ElementType.DO_EXPR, DoExpr.EXPR),
        ("AssertExpr", ElementType.ASSERT_EXPR, AssertExpr.EXPR),
        ("LazyExpr", ElementType.LAZY_EXPR, LazyExpr.EXPR),
        ("ComputationExpr", ElementType.COMPUTATION_EXPR, ComputationExpr.EXPR),
        ("SetExpr", ElementType.SET_EXPR, SetExpr.RIGHT_EXPR),
        ("TryFinally_TryExpr", ElementType.TRY_FINALLY_EXPR, TryFinallyExpr.TRY_EXPR),
        ("TryFinally_FinallyExpr", ElementType.TRY_FINALLY_EXPR, TryFinallyExpr.FINALLY_EXPR),
        ("TryWith_TryExpr", ElementType.TRY_WITH_EXPR, TryWithExpr.TRY_EXPR),
        ("IfThenExpr", ElementType.IF_THEN_ELSE_EXPR, IfThenElseExpr.THEN_EXPR),
        ("ElifThenExpr", ElementType.ELIF_EXPR, ElifExpr.THEN_EXPR),
        ("LambdaExprBody", ElementType.LAMBDA_EXPR, LambdaExpr.EXPR),
        ("MatchExpr_Expr", ElementType.MATCH_EXPR, MatchExpr.EXPR),
        ("MatchExpr_With", ElementType.MATCH_EXPR, MatchExpr.WITH),
      };

      var typeDeclarationIndentingRulesParameters = new[]
      {
        ("TypeDeclarationRepr", ElementType.F_SHARP_TYPE_DECLARATION, FSharpTypeDeclaration.TYPE_REPR),
        ("TypeDeclarationMemberList", ElementType.F_SHARP_TYPE_DECLARATION, FSharpTypeDeclaration.MEMBER_LIST),
        ("ClassReprTypeMemberList", ElementType.CLASS_REPRESENTATION, ClassRepresentation.MEMBER_LIST),
        ("StructReprTypeMemberList", ElementType.STRUCT_REPRESENTATION, StructRepresentation.MEMBER_LIST),
        ("InterfaceReprTypeMemberList", ElementType.INTERFACE_REPRESENTATION, InterfaceRepresentation.MEMBER_LIST),
        ("ExceptionMemberList", ElementType.EXCEPTION_DECLARATION, ExceptionDeclaration.MEMBER_LIST),
        ("InterfaceImplMemberList", ElementType.INTERFACE_IMPLEMENTATION, InterfaceImplementation.MEMBER_LIST),
        ("ModuleAbbreviationDeclaration", ElementType.MODULE_ABBREVIATION_DECLARATION, ModuleAbbreviationDeclaration.TYPE_REFERENCE),
      };

      bindingAndModuleDeclIndentingRulesParameters
        .Union(fsExprIndentingRulesParameters)
        .Union(typeDeclarationIndentingRulesParameters)
        .ToList()
        .ForEach(DescribeSimpleIndentingRule);

      Describe<ContinuousIndentRule>()
        .Name("ContinuousIndent")
        .Where(Node().In(ElementType.UNIT_EXPR, ElementType.UNIT_PAT))
        .AddException(Node().In(ElementType.ATTRIBUTE_LIST))
        .Build();
      
      Describe<IndentingRule>()
        .Name("SimpleTypeRepr_Accessibility")
        .Where(
          Parent().In(ElementBitsets.SIMPLE_TYPE_REPRESENTATION_BIT_SET),
          Left().In(ElementBitsets.ENUM_CASE_LIKE_DECLARATION_BIT_SET).Satisfies((node, context) =>
            AccessModifiers[node.GetPreviousMeaningfulSibling()?.GetTokenType()]))
        .CloseNodeGetter((node, context) => node.Parent?.LastChild)
        .Return(IndentType.External)
        .Build();

      Describe<IndentingRule>()
        .Name("TryWith_WithClauseIndent")
        .Where(
          Parent().HasType(ElementType.TRY_WITH_EXPR),
          Node().HasRole(TryWithExpr.MATCH_CLAUSE))
        .Switch(
          settings => settings.IndentOnTryWith,
          When(true).Return(IndentType.External),
          When(false).Return(IndentType.None))
        .Build();

      Describe<IndentingRule>()
        .Name("PrefixAppExprIndent")
        .Where(
          Parent().HasType(ElementType.PREFIX_APP_EXPR),
          Node()
            .HasRole(PrefixAppExpr.ARG_EXPR)
            .Satisfies((node, context) =>
              !(node is IComputationExpr) ||
              !node.ContainsLineBreak(context.CodeFormatter)))
        .Return(IndentType.External)
        .Build();

      Describe<IndentingRule>()
        .Name("ElseExprIndent")
        .Where(
          Parent().In(ElementType.IF_THEN_ELSE_EXPR, ElementType.ELIF_EXPR),
          Node()
            .HasRole(IfThenElseExpr.ELSE_CLAUSE)
            .Satisfies(IndentElseExpr)
            .Or()
            .HasRole(ElifExpr.ELSE_CLAUSE)
            .Satisfies(IndentElseExpr))
        .Return(IndentType.External)
        .Build();

      Describe<IndentingRule>()
        .Name("MatchClauseExprIndent")
        .Where(
          Node().HasRole(MatchClause.EXPR),
          Parent()
            .HasType(ElementType.MATCH_CLAUSE)
            .Satisfies((node, context) =>
            {
              if (!(node is IMatchClause matchClause))
                return false;

              var expr = matchClause.Expression;
              return !IsLastNodeOfItsType(node, context) ||
                     !AreAligned(matchClause, expr, context.CodeFormatter);
            }))
        .Return(IndentType.External)
        .Build();

      Describe<IndentingRule>()
        .Name("MatchClauseWhenExprIndent")
        .Where(
          Parent().HasType(ElementType.MATCH_CLAUSE),
          Node().HasRole(MatchClause.WHEN_CLAUSE))
        .Return(IndentType.External, 2)
        .Build();

      Describe<IndentingRule>()
        .Name("DoDeclIndent")
        .Where(
          Parent()
            .HasType(ElementType.DO_STATEMENT),
          Node().HasRole(DoStatement.CHAMELEON_EXPR))
        .Return(IndentType.External)
        .Build();
    }

    private void Aligning()
    {
      var alignmentRulesParameters = new[]
      {
        ("MatchClauses", ElementType.MATCH_EXPR),
        ("UnionRepresentation", ElementType.UNION_REPRESENTATION),
        ("EnumCases", ElementType.ENUM_REPRESENTATION),
        ("SequentialExpr", ElementType.SEQUENTIAL_EXPR),
        ("BinaryExpr", ElementType.BINARY_APP_EXPR),
        ("RecordDeclaration", ElementType.RECORD_FIELD_DECLARATION_LIST),
        ("RecordExprBindings", ElementType.RECORD_FIELD_BINDING_LIST),
        ("MemberDeclarationList", ElementType.MEMBER_DECLARATION_LIST),
        ("TypeMemberDeclarationList", ElementType.TYPE_MEMBER_DECLARATION_LIST),
      };

      alignmentRulesParameters
        .ToList()
        .ForEach(DescribeSimpleAlignmentRule);

      Describe<IndentingRule>().Name("EnumCaseLikeDeclarations")
        .Where(Parent().In(ElementBitsets.SIMPLE_TYPE_REPRESENTATION_BIT_SET),
          Left().In(ElementBitsets.ENUM_CASE_LIKE_DECLARATION_BIT_SET).Satisfies(IsFirstNodeOfItsType))
        .CloseNodeGetter((node, context) => node.Parent?.LastChild)
        .Return(IndentType.AlignThrough) // through => including the last node (till => without the last one)
        .Build();

      Describe<IndentingRule>()
        .Name("OutdentBinaryOperators")
        .Where(
          Parent().HasType(ElementType.BINARY_APP_EXPR),
          Node().HasRole(BinaryAppExpr.OP_REF_EXPR).Satisfies((node, _) => !IsPipeOperator(node, _)))
        .Switch(settings => settings.OutdentBinaryOperators,
          When(true).Return(IndentType.Outdent | IndentType.External))
        .Build();

      Describe<IndentingRule>()
        .Name("OutdentPipeOperators")
        .Where(
          Parent().HasType(ElementType.BINARY_APP_EXPR),
          Node().HasRole(BinaryAppExpr.OP_REF_EXPR).Satisfies(IsPipeOperator))
        .Switch(settings => settings.OutdentBinaryOperators,
          When(true).Switch(settings => settings.NeverOutdentPipeOperators,
            When(false).Return(IndentType.Outdent | IndentType.External)))
        .Build();
    }

    private void Formatting()
    {
      Describe<FormattingRule>()
        .Name("DeclarationsSpaces")
        .Group(SpaceRuleGroup)
        .Where(Parent().In(ElementType.ENUM_CASE_DECLARATION, ElementType.UNION_CASE_DECLARATION,
          ElementType.UNION_CASE_FIELD_DECLARATION_LIST))
        .Return(IntervalFormatType.Space)
        .Build();

      Describe<FormattingRule>()
        .Name("SpaceBeforeColon")
        .Group(SpaceRuleGroup)
        .Where(Right().In(FSharpTokenType.COLON))
        .Switch(it => it.SpaceBeforeColon, SpaceOptionsBuilders)
        .Build();

      Describe<FormattingRule>()
        .Name("NoSpaceInUnit")
        .Group(SpaceRuleGroup)
        .Where(
          Parent().In(ElementType.UNIT_EXPR, ElementType.UNIT_PAT),
          Left().In(FSharpTokenType.LPAREN),
          Right().In(FSharpTokenType.RPAREN))
        .Return(IntervalFormatType.OnlyEmpty)
        .Build();

      Describe<FormattingRule>()
        .Group(LineBreaksRuleGroup)
        .Name("LineBreakAfterTypeReprAccessModifier")
        .Where(
          Parent()
            .In(ElementBitsets.SIMPLE_TYPE_REPRESENTATION_BIT_SET)
            .Satisfies((node, context) => ((ISimpleTypeRepresentation) node).AccessModifier != null),
          Right().In(ElementBitsets.ENUM_CASE_LIKE_DECLARATION_BIT_SET).Satisfies(IsFirstNodeOfItsType))
        .Switch(settings => settings.LineBreakAfterTypeReprAccessModifier,
          When(true).Return(IntervalFormatType.NewLine))
        .Build();

      DescribeLineBreakInNode("TypeDeclaration", Node().In(ElementType.F_SHARP_TYPE_DECLARATION),
        Node().In(ElementBitsets.TYPE_REPRESENTATION_BIT_SET).Satisfies((node, context) =>
          node.GetPreviousMeaningfulSibling()?.GetTokenType() == FSharpTokenType.EQUALS),
        key => key.DeclarationBodyOnTheSameLine, key => key.KeepExistingLineBreakBeforeDeclarationBody);

      Describe<FormattingRule>()
        .Group(SpaceRuleGroup)
        .Name("SpaceAfterImplicitConstructorDecl")
        .Where(Left().HasType(ElementType.PRIMARY_CONSTRUCTOR_DECLARATION))
        .Return(IntervalFormatType.Space)
        .Build();

      Describe<FormattingRule>()
        .Group(SpaceRuleGroup)
        .Name("SpacesInMemberConstructorDecl")
        .Where(Parent().HasType(ElementType.SECONDARY_CONSTRUCTOR_DECLARATION))
        .Return(IntervalFormatType.Space)
        .Build();

      Describe<FormattingRule>()
        .Name("SpaceBetweenRecordBindings")
        .Where(
          Left()
            .HasType(ElementType.RECORD_FIELD_BINDING)
            .Satisfies((node, _) => ((IRecordFieldBinding) node).Semicolon != null),
          Right().HasType(ElementType.RECORD_FIELD_BINDING))
        .Return(IntervalFormatType.Space)
        .Build();

      Describe<FormattingRule>()
        .Group(LineBreaksRuleGroup)
        .Name("LineBreaksBetweenRecordBindings")
        .Where(
          Left()
            .HasType(ElementType.RECORD_FIELD_BINDING)
            .Satisfies((node, _) => ((IRecordFieldBinding) node).Semicolon == null),
          Right().HasType(ElementType.RECORD_FIELD_BINDING))
        .Return(IntervalFormatType.NewLine)
        .Build();

      Describe<FormattingRule>()
        .Name("SpacesAroundRecordExprBraces")
        .Where(
          Parent().HasType(ElementType.RECORD_EXPR),
          Left().In(FSharpTokenType.LBRACE, FSharpTokenType.RBRACE),
          Right()
            .In(ElementType.RECORD_FIELD_BINDING_LIST, FSharpTokenType.BLOCK_COMMENT)
            .Or()
            .HasRole(RecordExpr.COPY_INFO))
        .Return(IntervalFormatType.OnlySpace)
        .Build()
        .AndViceVersa()
        .Build();
    }

    private void DescribeSimpleIndentingRule((string name, CompositeNodeType parentType, short childRole) parameters)
    {
      Describe<IndentingRule>()
        .Name(parameters.name + "Indent")
        .Where(
          Parent().HasType(parameters.parentType),
          Node().HasRole(parameters.childRole))
        .Return(IndentType.External)
        .Build();
    }

    private void DescribeLineBreakInNode(string name,
      IBuilderAction<IBlankWithSinglePattern> containingNodesPattern,
      IBuilderAction<IBlankWithSinglePattern> equalsBeforeNodesPattern,
      Expression<Func<FSharpFormatSettingsKey, object>> onSameLine,
      Expression<Func<FSharpFormatSettingsKey, object>> keepExistingLineBreak)
    {
      var containingNodes = containingNodesPattern.BuildBlank();
      var equalsBeforeNodes = equalsBeforeNodesPattern.BuildBlank();

      Describe<WrapRule>()
        .Name($"{name}Wrap")
        .Where(Node().Is(containingNodes))
        .Switch(keepExistingLineBreak,
          When(false).Switch(onSameLine,
            When(PlaceOnSameLineAsOwner.IF_OWNER_IS_SINGLE_LINE)
              .Return(WrapType.Chop | WrapType.PseudoStartBeforeExternal)))
        .Build();

      Describe<FormattingRule>()
        .Name($"{name}NewLine")
        .Where(
          Parent().Is(containingNodes),
          Right().Is(equalsBeforeNodes))
        .Group(LineBreaksRuleGroup | WrapRuleGroup)
        .Switch(keepExistingLineBreak,
          When(true)
            .Switch(onSameLine,
              When(PlaceOnSameLineAsOwner.NEVER).Return(IntervalFormatType.NewLine),
              When(PlaceOnSameLineAsOwner.ALWAYS, PlaceOnSameLineAsOwner.IF_OWNER_IS_SINGLE_LINE)
                .Return(IntervalFormatType.DoNotRemoveUserNewLines)),
          When(false)
            .Switch(onSameLine,
              When(PlaceOnSameLineAsOwner.NEVER).Return(IntervalFormatType.NewLine),
              When(PlaceOnSameLineAsOwner.ALWAYS).Return(IntervalFormatType.RemoveUserNewLines),
              When(PlaceOnSameLineAsOwner.IF_OWNER_IS_SINGLE_LINE)
                .Return(IntervalFormatType.RemoveUserNewLines | IntervalFormatType.InsertNewLineConditionally)))
        .Build();

      // initial impl (keeps formatting instead of keeping existing line break only):
      // .Switch(keepExistingLineBreak,
      // When(true).Return(IntervalFormatType.DoNotRemoveUserNewLines),
      // When(false)
      //   .Switch(onSameLine,
      //     When(PlaceOnSameLineAsOwner.NEVER).Return(IntervalFormatType.NewLine),
      //     When(PlaceOnSameLineAsOwner.ALWAYS).Return(IntervalFormatType.RemoveUserNewLines),
      //     When(PlaceOnSameLineAsOwner.IF_OWNER_IS_SINGLE_LINE)
      //       .Return(IntervalFormatType.RemoveUserNewLines | IntervalFormatType.InsertNewLineConditionally)))

      Describe<FormattingRule>()
        .Name($"{name}Space")
        .Where(
          Parent().Is(containingNodes),
          Right().Is(equalsBeforeNodes))
        .Group(SpaceRuleGroup)
        .Return(IntervalFormatType.Space)
        .Priority(3)
        .Build();
    }

    private void DescribeSimpleAlignmentRule((string name, CompositeNodeType nodeType) parameters)
    {
      Describe<IndentingRule>()
        .Name(parameters.name + "Alignment")
        .Where(Node().HasType(parameters.nodeType))
        .Return(IndentType.AlignThrough)
        .Build();
    }

    private static bool IndentElseExpr(ITreeNode elseExpr, CodeFormattingContext context) =>
      elseExpr.GetPreviousMeaningfulSibling().IsFirstOnLine(context.CodeFormatter) && !(elseExpr is IElifExpr);

    private static bool AreAligned(ITreeNode first, ITreeNode second, IWhitespaceChecker whitespaceChecker) =>
      first.CalcLineIndent(whitespaceChecker) == second.CalcLineIndent(whitespaceChecker);

    private static bool IsPipeOperator(ITreeNode node, CodeFormattingContext context) =>
      node is IReferenceExpr refExpr && FSharpPredefinedType.PipeOperatorNames.Contains(refExpr.ShortName);
  }
}
