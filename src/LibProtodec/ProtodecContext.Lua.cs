// Copyright © 2024 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using CommunityToolkit.Diagnostics;
using Loretta.CodeAnalysis;
using Loretta.CodeAnalysis.Lua.Syntax;

namespace LibProtodec;

partial class ProtodecContext
{
    public void ParseLuaSyntaxTree(SyntaxTree ast)
    {
        CompilationUnitSyntax root = (CompilationUnitSyntax)ast.GetRoot();
        SyntaxList<StatementSyntax> statements = root.Statements.Statements;

        LocalVariableDeclarationStatementSyntax pbTableDeclaration =
            (LocalVariableDeclarationStatementSyntax)statements[0];

        string pbTableName = pbTableDeclaration.Names[0].Name;
        Guard.IsTrue(
            pbTableName.EndsWith("_pbTable"));

        bool importedProtobufLib = false;
        string? returns = null;

        foreach (StatementSyntax statement in statements)
        {
            switch (statement)
            {
                case LocalVariableDeclarationStatementSyntax
                {
                    Names: [{ Name: { } varName }],
                    EqualsValues.Values: [FunctionCallExpressionSyntax { Expression: IdentifierNameSyntax { Name: "require" } } call]
                }:
                    switch (call.Argument)
                    {
                        case StringFunctionArgumentSyntax { Expression.Token.ValueText: "protobuf/protobuf" }:
                            importedProtobufLib = true;
                            break;
                        case ExpressionListFunctionArgumentSyntax { Expressions: [LiteralExpressionSyntax { Token.ValueText: {} import }] }:
                            // TODO: handle imported protos
                            break;
                    }
                    break;
                case ExpressionStatementSyntax
                {
                    Expression: FunctionCallExpressionSyntax
                    {
                        Expression: IdentifierNameSyntax { Name: "module" },
                        Argument: ExpressionListFunctionArgumentSyntax
                        {
                            Expressions: [LiteralExpressionSyntax literal]
                        }
                    }
                }:
                    string moduleName = literal.Token.ValueText;
                    break;
                case AssignmentStatementSyntax
                {
                    Variables: [MemberAccessExpressionSyntax varExpr],
                    EqualsValues.Values: [{} valueExpr]
                }:
                    // TODO: build protos
                    break;
                case ReturnStatementSyntax { Expressions: [IdentifierNameSyntax identifier] }:
                    returns = identifier.Name;
                    break;
            }
        }

        if (!importedProtobufLib || returns != pbTableName)
        {
            ThrowHelper.ThrowInvalidDataException();
        }
    }
}