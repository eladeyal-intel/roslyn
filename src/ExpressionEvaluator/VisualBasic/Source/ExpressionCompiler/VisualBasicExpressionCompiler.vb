﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.ExpressionEvaluator
Imports Microsoft.VisualStudio.Debugger
Imports Microsoft.VisualStudio.Debugger.Clr
Imports Microsoft.VisualStudio.Debugger.Evaluation

Namespace Microsoft.CodeAnalysis.VisualBasic.ExpressionEvaluator

    <DkmReportNonFatalWatsonException(ExcludeExceptionType:=GetType(NotImplementedException)), DkmContinueCorruptingException>
    Friend NotInheritable Class VisualBasicExpressionCompiler
        Inherits ExpressionCompiler

        Private Shared ReadOnly s_compilerId As New DkmCompilerId(DkmVendorId.Microsoft, DkmLanguageId.VB)

        Public Sub New()
            MyBase.New(New VisualBasicFrameDecoder(), New VisualBasicLanguageInstructionDecoder())
        End Sub

        Friend Overrides ReadOnly Property DiagnosticFormatter As DiagnosticFormatter
            Get
                Return DebuggerDiagnosticFormatter.Instance
            End Get
        End Property

        Friend Overrides ReadOnly Property CompilerId As DkmCompilerId
            Get
                Return s_compilerId
            End Get
        End Property

        Friend Delegate Function GetMetadataContextDelegate(Of TAppDomain)(appDomain As TAppDomain) As AppDomainMetadataContext(Of VisualBasicCompilation, EvaluationContext)
        Friend Delegate Sub SetMetadataContextDelegate(Of TAppDomain)(appDomain As TAppDomain, metadataContext As AppDomainMetadataContext(Of VisualBasicCompilation, EvaluationContext))

        Friend Overrides Function CreateTypeContext(
            appDomain As DkmClrAppDomain,
            metadataBlocks As ImmutableArray(Of MetadataBlock),
            moduleVersionId As Guid,
            typeToken As Integer,
            useReferencedModulesOnly As Boolean) As EvaluationContextBase

            Return CreateTypeContextHelper(
                appDomain,
                Function(ad) ad.GetMetadataContext(Of AppDomainMetadataContext(Of VisualBasicCompilation, EvaluationContext))(),
                metadataBlocks,
                moduleVersionId,
                typeToken,
                useReferencedModulesOnly)
        End Function

        Friend Shared Function CreateTypeContextHelper(Of TAppDomain)(
            appDomain As TAppDomain,
            getMetadataContext As GetMetadataContextDelegate(Of TAppDomain),
            metadataBlocks As ImmutableArray(Of MetadataBlock),
            moduleVersionId As Guid,
            typeToken As Integer,
            useReferencedModulesOnly As Boolean) As EvaluationContextBase

            If useReferencedModulesOnly Then
                ' Avoid using the cache for referenced assemblies only
                ' since this should be the exceptional case.
                Dim compilation = metadataBlocks.ToCompilationReferencedModulesOnly(moduleVersionId)
                Return EvaluationContext.CreateTypeContext(
                    compilation,
                    moduleVersionId,
                    typeToken)
            End If

            Dim previous = getMetadataContext(appDomain)
            If Not previous.Matches(metadataBlocks) Then
                previous = Nothing
            End If
            If previous IsNot Nothing AndAlso previous.ModuleVersionId <> moduleVersionId Then
                previous = Nothing
            End If

            Dim previousContext = previous?.AssemblyContext
            Dim context = EvaluationContext.CreateTypeContext(
                previousContext,
                metadataBlocks,
                moduleVersionId,
                typeToken)

            ' New type context is not attached to the AppDomain since it is less
            ' re-usable than the previous attached method context. (We could hold
            ' on to it if we don't have a previous method context but it's unlikely
            ' that we evaluated a type-level expression before a method-level.)
            Debug.Assert(context IsNot previousContext?.EvaluationContext)

            Return context
        End Function

        Friend Overrides Function CreateMethodContext(
            appDomain As DkmClrAppDomain,
            metadataBlocks As ImmutableArray(Of MetadataBlock),
            lazyAssemblyReaders As Lazy(Of ImmutableArray(Of AssemblyReaders)),
            symReader As Object,
            moduleVersionId As Guid,
            methodToken As Integer,
            methodVersion As Integer,
            ilOffset As UInteger,
            localSignatureToken As Integer,
            useReferencedModulesOnly As Boolean) As EvaluationContextBase

            Return CreateMethodContextHelper(
                appDomain,
                Function(ad) ad.GetMetadataContext(Of AppDomainMetadataContext(Of VisualBasicCompilation, EvaluationContext))(),
                Sub(ad, mc) ad.SetMetadataContext(Of AppDomainMetadataContext(Of VisualBasicCompilation, EvaluationContext))(mc),
                metadataBlocks,
                lazyAssemblyReaders,
                symReader,
                moduleVersionId,
                methodToken,
                methodVersion,
                ilOffset,
                localSignatureToken,
                useReferencedModulesOnly)
        End Function

        Friend Shared Function CreateMethodContextHelper(Of TAppDomain)(
            appDomain As TAppDomain,
            getMetadataContext As GetMetadataContextDelegate(Of TAppDomain),
            setMetadataContext As SetMetadataContextDelegate(Of TAppDomain),
            metadataBlocks As ImmutableArray(Of MetadataBlock),
            lazyAssemblyReaders As Lazy(Of ImmutableArray(Of AssemblyReaders)),
            symReader As Object,
            moduleVersionId As Guid,
            methodToken As Integer,
            methodVersion As Integer,
            ilOffset As UInteger,
            localSignatureToken As Integer,
            useReferencedModulesOnly As Boolean) As EvaluationContextBase

            If useReferencedModulesOnly Then
                ' Avoid using the cache for referenced assemblies only
                ' since this should be the exceptional case.
                Dim compilation = metadataBlocks.ToCompilationReferencedModulesOnly(moduleVersionId)
                Return EvaluationContext.CreateMethodContext(
                    compilation,
                    lazyAssemblyReaders,
                    symReader,
                    moduleVersionId,
                    methodToken,
                    methodVersion,
                    ilOffset,
                    localSignatureToken)
            End If

            Dim previous = getMetadataContext(appDomain)
            If Not previous.Matches(metadataBlocks) Then
                previous = Nothing
            End If
            If previous IsNot Nothing AndAlso previous.ModuleVersionId <> moduleVersionId Then
                previous = Nothing
            End If

            Dim previousContext = previous?.AssemblyContext
            Dim context = EvaluationContext.CreateMethodContext(
                previousContext,
                metadataBlocks,
                lazyAssemblyReaders,
                symReader,
                moduleVersionId,
                methodToken,
                methodVersion,
                ilOffset,
                localSignatureToken,
                useReferencedAssembliesOnly:=True)

            If context IsNot previousContext?.EvaluationContext Then
                setMetadataContext(
                    appDomain,
                    New AppDomainMetadataContext(Of VisualBasicCompilation, EvaluationContext)(
                        metadataBlocks,
                        moduleVersionId,
                        New VisualBasicMetadataContext(context.Compilation, context)))
            End If

            Return context
        End Function

        Friend Overrides Sub RemoveDataItem(appDomain As DkmClrAppDomain)
            appDomain.RemoveMetadataContext(Of AppDomainMetadataContext(Of VisualBasicCompilation, EvaluationContext))()
        End Sub

        Friend Overrides Function GetMetadataBlocks(appDomain As DkmClrAppDomain, runtimeInstance As DkmClrRuntimeInstance) As ImmutableArray(Of MetadataBlock)
            Dim previous = appDomain.GetMetadataContext(Of AppDomainMetadataContext(Of VisualBasicCompilation, EvaluationContext))()
            Return runtimeInstance.GetMetadataBlocks(appDomain, previous.MetadataBlocks)
        End Function

    End Class

End Namespace
