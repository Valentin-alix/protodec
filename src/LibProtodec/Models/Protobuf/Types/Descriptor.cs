// Copyright © 2024 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using CommunityToolkit.Diagnostics;
using LibProtodec.Models.Protobuf.TopLevels;

namespace LibProtodec.Models.Protobuf.Types;

public sealed class Descriptor : IProtobufType
{
    public int TypeIndex { get; set; }
    public bool IsRepeated { get; set; }
    public /*TopLevel*/IProtobufType? TopLevelType { get; set; }

    public string Name =>
        Type.Name;

    public IProtobufType Type
    {
        get
        {
            IProtobufType type = TopLevelType as IProtobufType ?? TypeIndex switch
            {
                1  => Scalar.Double,
                2  => Scalar.Float,
                3  => Scalar.Int64,
                4  => Scalar.UInt64,
                5  => Scalar.Int32,
                6  => Scalar.Fixed64,
                7  => Scalar.Fixed32,
                8  => Scalar.Bool,
                9  => Scalar.String,
                10 => ThrowHelper.ThrowNotSupportedException<IProtobufType>("Parsing proto2 groups are not supported. Open an issue if you need this."),
                12 => Scalar.Bytes,
                13 => Scalar.UInt32,
                15 => Scalar.SFixed32,
                16 => Scalar.SFixed64,
                17 => Scalar.SInt32,
                18 => Scalar.SInt64,
                _ => ThrowHelper.ThrowArgumentOutOfRangeException<IProtobufType>()
            };

            if (IsRepeated)
            {
                type = new Repeated(type);
            }

            return type;
        }
    }
}