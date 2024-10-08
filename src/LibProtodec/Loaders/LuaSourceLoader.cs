﻿// Copyright © 2024 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Loretta.CodeAnalysis;
using Loretta.CodeAnalysis.Lua;
using Loretta.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace LibProtodec.Loaders;

public sealed class LuaSourceLoader
{
    public IReadOnlyDictionary<string, SyntaxTree> LoadedSyntaxTrees { get; }

    public LuaSourceLoader(string sourcePath, ILogger<LuaSourceLoader>? logger = null)
    {
        LoadedSyntaxTrees = File.Exists(sourcePath)
            ? new Dictionary<string, SyntaxTree> { { Path.GetFileNameWithoutExtension(sourcePath) , LoadSyntaxTreeFromSourceFile(sourcePath) } }
            : Directory.EnumerateFiles(sourcePath, searchPattern: "*.lua")
                       .Select(static sourcePath =>
                           (Path.GetFileNameWithoutExtension(sourcePath), LoadSyntaxTreeFromSourceFile(sourcePath)))
                       .ToDictionary();

        logger?.LogLoadedLuaSyntaxTrees(LoadedSyntaxTrees.Count);
    }

    public SyntaxTree ResolveImport(string import)
    {
        import = Path.GetFileNameWithoutExtension(import);

        return LoadedSyntaxTrees[import];
    }

    private static SyntaxTree LoadSyntaxTreeFromSourceFile(string filePath)
    {
        using FileStream fileStream = File.OpenRead(filePath);

        return LuaSyntaxTree.ParseText(
            SourceText.From(fileStream),
            null, // TODO: maybe expose this as a parameter
            Path.GetFileName(filePath));
        //TODO: consider checking lua source validity via SyntaxTree.GetDiagnostics()
    }
}