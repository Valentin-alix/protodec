﻿// Copyright © 2024 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace LibProtodec.Models.Types;

public sealed class Repeated(IType type) : IType
{
    public string Name =>
        $"repeated {type.Name}";
}