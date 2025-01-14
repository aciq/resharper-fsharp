namespace JetBrains.ReSharper.Plugins.FSharp.Psi.Features.CodeCompletion.Rules

open FSharp.Compiler.Symbols
open JetBrains.ReSharper.Feature.Services.CodeCompletion.Infrastructure
open JetBrains.ReSharper.Feature.Services.CodeCompletion.Infrastructure.LookupItems
open JetBrains.ReSharper.Plugins.FSharp.Psi
open JetBrains.ReSharper.Plugins.FSharp.Psi.Features.CodeCompletion
open JetBrains.ReSharper.Plugins.FSharp.Psi.Features.CodeCompletion.FSharpCompletionUtil
open JetBrains.ReSharper.Psi

[<Language(typeof<FSharpLanguage>)>]
type FSharpRelevanceRule() =
    inherit ItemsProviderOfSpecificContext<FSharpCodeCompletionContext>()

    override this.DecorateItems(_, items) =
        for item in items do
            let fcsLookupItem = item.As<FcsLookupItem>()
            if isNull fcsLookupItem then () else

            match fcsLookupItem.FcsSymbol with
            | :? FSharpEntity ->
                markRelevance item CLRLookupItemRelevance.TypesAndNamespaces

                if not (Array.isEmpty fcsLookupItem.NamespaceToOpen) then
                    markRelevance item CLRLookupItemRelevance.NotImportedType
                else
                    markRelevance item CLRLookupItemRelevance.ImportedType

            | :? FSharpMemberOrFunctionOrValue as mfv ->
                if not mfv.IsModuleValueOrMember then
                    markRelevance item CLRLookupItemRelevance.LocalVariablesAndParameters else

                if mfv.IsEvent then
                    markRelevance item CLRLookupItemRelevance.Events else

                if mfv.IsExtensionMember then
                    markRelevance item CLRLookupItemRelevance.ExtensionMethods else

                if mfv.IsMember && mfv.IsProperty then
                    markRelevance item CLRLookupItemRelevance.FieldsAndProperties
                else
                    markRelevance item CLRLookupItemRelevance.Methods

            | :? FSharpField as field when
                    field.DeclaringEntity
                    |> Option.map (fun e -> e.IsEnum)
                    |> Option.defaultValue false ->
                markRelevance item CLRLookupItemRelevance.EnumMembers

            | :? FSharpField ->
                markRelevance item CLRLookupItemRelevance.FieldsAndProperties

            | :? FSharpUnionCase
            | :? FSharpActivePatternCase ->
                markRelevance item CLRLookupItemRelevance.Methods

            | :? FSharpParameter -> markRelevance item CLRLookupItemRelevance.LocalVariablesAndParameters

            | :? FSharpGenericParameter
            | :? FSharpStaticParameter -> markRelevance item CLRLookupItemRelevance.TypesAndNamespaces

            | _ -> ()
