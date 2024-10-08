// Copyright © 2024 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommunityToolkit.Diagnostics;
using LibProtodec.Models.Protobuf.TopLevels;

namespace LibProtodec.Models.Protobuf;

public sealed class Protobuf
{
    private int _version = 3;
    private string? _fileName;
    private HashSet<string>? _imports;

    public readonly List<TopLevel> TopLevels = [];

    public string? AssemblyName { get; init; }
    public string? SourceName   { get; init; }
    public string? Edition      { get; set;  }
    public string? CilNamespace { get; init; }

    public string FileName
    {
        get => _fileName ??= $"{string.Join('_', TopLevels.Select(static topLevel => topLevel.Name))}.proto";
        set => _fileName = value;
    }

    public int Version
    {
        get => _version;
        set
        {
            Guard.IsBetweenOrEqualTo(value, 2, 3);
            _version = value;
        }
    }

    public HashSet<string> Imports =>
        _imports ??= [];

    public void WriteTo(IndentedTextWriter writer)
    {
        writer.WriteLine("// Decompiled with protodec");

        if (AssemblyName is not null)
        {
            writer.Write("// Assembly: ");
            writer.WriteLine(AssemblyName);
        }

        if (SourceName is not null)
        {
            writer.Write("// Source: ");
            writer.WriteLine(SourceName);
        }

        writer.WriteLine();
        writer.WriteLine(
            Edition is null
                ? $"""syntax = "proto{Version}";"""
                : $"""edition = "{Edition}";""");

        if (_imports is not null)
        {
            foreach (string import in _imports)
            {
                writer.WriteLine();
                writer.Write("import \"");
                writer.Write(import);
                writer.Write("\";");
            }
        }

        if (CilNamespace is not null)
        {
            writer.WriteLine();
            writer.WriteLine();
            WriteOptionTo(writer, "csharp_namespace", CilNamespace, true);
        }

        foreach (TopLevel topLevel in TopLevels)
        {
            writer.WriteLine();
            writer.WriteLine();
            topLevel.WriteTo(writer);
        }
    }

    public static void WriteOptionTo(TextWriter writer, string name, string value, bool quoteValue = false)
    {
        writer.Write("option ");
        writer.Write(name);
        writer.Write(" = ");

        if (quoteValue)
        {
            writer.Write('\"');
        }

        writer.Write(value);

        if (quoteValue)
        {
            writer.Write('\"');
        }

        writer.Write(';');
    }
}