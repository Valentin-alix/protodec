// Copyright © 2024 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using CommunityToolkit.Diagnostics;

namespace LibProtodec.Models.Protobuf.Fields;

public sealed class EnumField
{
    public string? Name { get; set; }
    public int     Id   { get; set; }

    public bool IsObsolete { get; init; }

    public void WriteTo(System.IO.TextWriter writer)
    {
        Guard.IsNotNull(Name);

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