// Copyright © 2024 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.IO;
using CommunityToolkit.Diagnostics;
using LibProtodec.Models.Protobuf.TopLevels;
using LibProtodec.Models.Protobuf.Types;

namespace LibProtodec.Models.Protobuf.Fields;

public sealed class MessageField
{
    private bool? _isRepeated;

    public IProtobufType? Type { get; set; }
    public Message? DeclaringMessage { get; set; }

    public string? Name { get; set; }
    public int     Id   { get; set; }

    public bool IsOptional { get; set;  }
    public bool IsRequired { get; set;  }
    public bool IsObsolete { get; init; }
    public bool HasHasProp { get; init; }
    public bool IsRepeated
    {
        get => _isRepeated ??= Type is Repeated;
        set => _isRepeated = value;
    }

    public void WriteTo(TextWriter writer, bool isOneOf)
    {
        Guard.IsNotNull(Type);
        Guard.IsNotNull(Name);
        Guard.IsNotNull(DeclaringMessage);

        if (IsOptional || (HasHasProp && !isOneOf && !IsRepeated))
        {
            writer.Write("optional ");
        }

        if (IsRequired)
        {
            writer.Write("required ");
        }

        writer.Write(
            DeclaringMessage.QualifyTypeName(Type));
        writer.Write(' ');
        writer.Write(Name);
        writer.Write(" = ");
        writer.Write(Id);

        if (IsObsolete)
        {
            writer.Write(" [deprecated = true]");
        }

        writer.WriteLine(';');
    }
}