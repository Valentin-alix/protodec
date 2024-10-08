// Copyright © 2024 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommunityToolkit.Diagnostics;
using LibProtodec.Models.Protobuf;
using LibProtodec.Models.Protobuf.Fields;
using LibProtodec.Models.Protobuf.TopLevels;
using LibProtodec.Models.Protobuf.Types;
using Loretta.CodeAnalysis;
using Loretta.CodeAnalysis.Lua.Syntax;
using SystemEx;

namespace LibProtodec;

partial class ProtodecContext
{
    public Protobuf ParseLuaSyntaxTree(SyntaxTree ast)
    {
        CompilationUnitSyntax root = (CompilationUnitSyntax)ast.GetRoot();
        SyntaxList<StatementSyntax> statements = root.Statements.Statements;

        bool importedProtobufLib = false;
        Dictionary<string, object> pbTable = [];
        Protobuf protobuf = new()
        {
            Version    = 2,
            SourceName = ast.FilePath
        };

        foreach (StatementSyntax statement in statements)
        {
            switch (statement)
            {
                case LocalVariableDeclarationStatementSyntax
                {
                    EqualsValues.Values: [FunctionCallExpressionSyntax { Expression: IdentifierNameSyntax { Name: "require" } } call]
                }:
                    switch (call.Argument)
                    {
                        case StringFunctionArgumentSyntax { Expression.Token.ValueText: "protobuf/protobuf" }:
                            importedProtobufLib = true;
                            break;
                        case ExpressionListFunctionArgumentSyntax { Expressions: [LiteralExpressionSyntax { Token.ValueText: {} import }] }:
                            import = Path.GetFileNameWithoutExtension(import).TrimEnd("_pb"); //todo: handle imports properly
                            protobuf.Imports.Add($"{import}.proto");
                            break;
                    }
                    break;
                case AssignmentStatementSyntax
                {
                    Variables: [MemberAccessExpressionSyntax
                    {
                        Expression: IdentifierNameSyntax,
                        MemberName.ValueText: {} tableKey
                    }],
                    EqualsValues.Values: [FunctionCallExpressionSyntax { Expression: MemberAccessExpressionSyntax { MemberName.ValueText: {} factory } }]
                }:
                    switch (factory)
                    {
                        case "Descriptor":
                            Message message = new()
                            {
                                Name     = tableKey,
                                Protobuf = protobuf
                            };
                            protobuf.TopLevels.Add(message);
                            pbTable.Add(tableKey, message);
                            break;
                        case "EnumDescriptor":
                            Enum @enum = new()
                            {
                                Name     = tableKey,
                                Protobuf = protobuf
                            };
                            protobuf.TopLevels.Add(@enum);
                            pbTable.Add(tableKey, @enum);
                            break;
                        case "FieldDescriptor":
                            pbTable.Add(tableKey, new MessageField { Type = new Descriptor() });
                            break;
                        case "EnumValueDescriptor":
                            pbTable.Add(tableKey, new EnumField());
                            break;
                    }
                    break;
                case AssignmentStatementSyntax
                {
                    Variables: [MemberAccessExpressionSyntax
                    {
                        Expression: MemberAccessExpressionSyntax { MemberName.ValueText: {} tableKey },
                        MemberName.ValueText: { } memberName
                    }],
                    EqualsValues.Values: [{ } valueExpr]
                }:
                    var valueTableElements =
                        (valueExpr as TableConstructorExpressionSyntax)?.Fields
                        .Cast<UnkeyedTableFieldSyntax>()
                        .Select(static element => element.Value);
                    object? valueLiteral = (valueExpr as LiteralExpressionSyntax)?.Token.Value;

                    switch (pbTable[tableKey])
                    {
                        case Message message:
                            switch (memberName)
                            {
                                case "name":
                                    message.Name = (string)valueLiteral!;
                                    break;
                                case "fields":
                                    foreach (MessageField messageField in valueTableElements!
                                                 .Cast<MemberAccessExpressionSyntax>()
                                                 .Select(x => pbTable[x.MemberName.ValueText])
                                                 .Cast<MessageField>())
                                    {
                                        messageField.DeclaringMessage = message;
                                        message.Fields.Add(messageField.Id, messageField);
                                    }
                                    break;
                                case "nested_types":
                                    if (valueTableElements!.Any())
                                        Logger?.LogNotImplemented("Parsing nested messages from lua");
                                    break;
                                case "enum_types":
                                    if (valueTableElements!.Any())
                                        Logger?.LogNotImplemented("Parsing nested enums from lua");
                                    break;
                                case "is_extendable":
                                    if ((bool)valueLiteral!)
                                        Logger?.LogNotImplemented("Parsing message extensions from lua");
                                    break;
                            }
                            break;
                        case Enum @enum:
                            switch (memberName)
                            {
                                case "name":
                                    @enum.Name = (string)valueLiteral!;
                                    break;
                                case "values":
                                    @enum.Fields.AddRange(
                                        valueTableElements!
                                            .Cast<MemberAccessExpressionSyntax>()
                                            .Select(x => pbTable[x.MemberName.ValueText])
                                            .Cast<EnumField>());
                                    break;
                            }
                            break;
                        case MessageField messageField:
                            Descriptor descriptor = (Descriptor)messageField.Type;
                            switch (memberName)
                            {
                                case "name":
                                    messageField.Name = (string)valueLiteral!;
                                    break;
                                case "number":
                                    messageField.Id = (int)(double)valueLiteral!;
                                    break;
                                case "label":
                                    switch ((int)(double)valueLiteral!)
                                    {
                                        case 1:
                                            messageField.IsOptional = true;
                                            break;
                                        case 2:
                                            messageField.IsRequired = true;
                                            break;
                                        case 3:
                                            descriptor.IsRepeated = true;
                                            break;
                                    }
                                    break;
                                case "type":
                                    descriptor.TypeIndex = (int)(double)valueLiteral!;
                                    break;
                                case "message_type":
                                case "enum_type":
                                    string typeTableKey = ((MemberAccessExpressionSyntax)valueExpr).MemberName.ValueText;
                                    if (pbTable.TryGetValue(typeTableKey, out object? topLevel)) //TODO: if this is false, then the top level is from an import, we will need to handle this
                                        descriptor.TopLevelType = (IProtobufType)topLevel;
                                    else
                                        descriptor.TopLevelType = new Scalar(typeTableKey); // temporary hack
                                    break;
                                case "has_default_value":
                                    if ((bool)valueLiteral!)
                                        Logger?.LogNotImplemented("Parsing default field values from lua");
                                    break;
                            }
                            break;
                        case EnumField enumField:
                            switch (memberName)
                            {
                                case "name":
                                    enumField.Name = (string)valueLiteral!;
                                    break;
                                case "number" when valueExpr is UnaryExpressionSyntax { Operand: LiteralExpressionSyntax { Token.Value: { } negativeNumber } }:
                                    enumField.Id = -(int)(double)negativeNumber;
                                    break;
                                case "number":
                                    enumField.Id = (int)(double)valueLiteral!;
                                    break;
                            }
                            break;
                    }
                    break;
                case ReturnStatementSyntax { Expressions: [IdentifierNameSyntax identifier] }:
                    protobuf.FileName = $"{identifier.Name.TrimEnd("_pbTable")}.proto";
                    break;
            }
        }

        if (!importedProtobufLib)
        {
            ThrowHelper.ThrowInvalidDataException();
        }

        this.Protobufs.Add(protobuf);
        return protobuf;
    }
}