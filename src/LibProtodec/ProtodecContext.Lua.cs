// Copyright © 2024 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections.Generic;
using System.Linq;
using SystemEx;
using CommunityToolkit.Diagnostics;
using LibProtodec.Loaders;
using LibProtodec.Models.Protobuf;
using LibProtodec.Models.Protobuf.Fields;
using LibProtodec.Models.Protobuf.TopLevels;
using LibProtodec.Models.Protobuf.Types;
using Loretta.CodeAnalysis;
using Loretta.CodeAnalysis.Lua.Syntax;
using FdpTypes = Google.Protobuf.Reflection.FieldDescriptorProto.Types;

namespace LibProtodec;

// TODO: add debug logging
partial class ProtodecContext
{
    private readonly Dictionary<string, Dictionary<string, object>> _parsedPbTables = [];
    private readonly Dictionary<string, Protobuf> _parsedProtobufs = [];

    public Protobuf ParseLuaSyntaxTree(LuaSourceLoader loader, SyntaxTree ast, ParserOptions options = ParserOptions.None)
    {
        if (_parsedProtobufs.TryGetValue(ast.FilePath, out Protobuf? parsedProto))
        {
            return parsedProto;
        }

        CompilationUnitSyntax root = (CompilationUnitSyntax)ast.GetRoot();
        SyntaxList<StatementSyntax> statements = root.Statements.Statements;

        bool importedProtobufLib = false;
        Dictionary<string, object> pbTable = _parsedPbTables[ast.FilePath] = [];
        Dictionary<string, Dictionary<string, object>> imports = [];
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
                    Names: [{ IdentifierName.Name: { } importKey }],
                    EqualsValues.Values: [FunctionCallExpressionSyntax { Expression: IdentifierNameSyntax { Name: "require" } } call]
                }:
                    switch (call.Argument)
                    {
                        case StringFunctionArgumentSyntax { Expression.Token.ValueText: "protobuf/protobuf" }:
                            importedProtobufLib = true;
                            break;
                        case ExpressionListFunctionArgumentSyntax { Expressions: [LiteralExpressionSyntax { Token.ValueText: { } import }] }:
                            SyntaxTree importedAst = loader.ResolveImport(import);
                            Protobuf importedProto = ParseLuaSyntaxTree(loader, importedAst);
                            imports.Add(importKey, _parsedPbTables[importedAst.FilePath]);
                            protobuf.Imports.Add(importedProto.FileName);
                            break;
                    }
                    break;
                case AssignmentStatementSyntax
                {
                    Variables: [MemberAccessExpressionSyntax
                    {
                        Expression: IdentifierNameSyntax,
                        MemberName.ValueText: { } tableKey
                    }],
                    EqualsValues.Values: [FunctionCallExpressionSyntax { Expression: MemberAccessExpressionSyntax { MemberName.ValueText: { } factory } }]
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
                            pbTable.Add(tableKey, new MessageField());
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
                        Expression: MemberAccessExpressionSyntax { MemberName.ValueText: { } tableKey },
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
                            switch (memberName)
                            {
                                case "name":
                                    messageField.Name = (string)valueLiteral!;
                                    break;
                                case "number":
                                    messageField.Id = (int)(double)valueLiteral!;
                                    break;
                                case "label":
                                    switch ((FdpTypes.Label)(double)valueLiteral!)
                                    {
                                        case FdpTypes.Label.Optional:
                                            messageField.IsOptional = true;
                                            break;
                                        case FdpTypes.Label.Required:
                                            messageField.IsRequired = true;
                                            break;
                                        case FdpTypes.Label.Repeated:
                                            messageField.IsRepeated = true;
                                            break;
                                    }
                                    break;
                                case "enum_type" when (options & ParserOptions.SkipEnums) > 0:
                                    messageField.Type = Scalar.Int32;
                                    break;
                                case "enum_type":
                                case "message_type":
                                    MemberAccessExpressionSyntax memberAccessExpr = (MemberAccessExpressionSyntax)valueExpr;
                                    string importKey = ((IdentifierNameSyntax)memberAccessExpr.Expression).Name;
                                    Dictionary<string, object> table = imports.GetValueOrDefault(importKey, pbTable);
                                    string typeTableKey = memberAccessExpr.MemberName.ValueText;
                                    IProtobufType scalar = (IProtobufType)table[typeTableKey];
                                    messageField.Type = messageField.IsRepeated
                                        ? new Repeated(scalar)
                                        : scalar;
                                    break;
                                case "type":
                                    messageField.Type ??= ParseFieldType(
                                        (FdpTypes.Type)(double)valueLiteral!, messageField.IsRepeated);
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
        _parsedProtobufs.Add(ast.FilePath, protobuf);
        return protobuf;
    }

    protected static IProtobufType ParseFieldType(FdpTypes.Type type, bool isRepeated)
    {
        IProtobufType scalar = type switch
        {
            FdpTypes.Type.Double   => Scalar.Double,
            FdpTypes.Type.Float    => Scalar.Float,
            FdpTypes.Type.Int64    => Scalar.Int64,
            FdpTypes.Type.Uint64   => Scalar.UInt64,
            FdpTypes.Type.Int32    => Scalar.Int32,
            FdpTypes.Type.Fixed64  => Scalar.Fixed64,
            FdpTypes.Type.Fixed32  => Scalar.Fixed32,
            FdpTypes.Type.Bool     => Scalar.Bool,
            FdpTypes.Type.String   => Scalar.String,
            FdpTypes.Type.Bytes    => Scalar.Bytes,
            FdpTypes.Type.Uint32   => Scalar.UInt32,
            FdpTypes.Type.Sfixed32 => Scalar.SFixed32,
            FdpTypes.Type.Sfixed64 => Scalar.SFixed64,
            FdpTypes.Type.Sint32   => Scalar.SInt32,
            FdpTypes.Type.Sint64   => Scalar.SInt64,
            FdpTypes.Type.Group    => ThrowHelper.ThrowNotSupportedException<IProtobufType>(
                "Parsing proto2 groups are not supported. Open an issue if you need this."),
        };

        return isRepeated
            ? new Repeated(scalar)
            : scalar;
    }
}