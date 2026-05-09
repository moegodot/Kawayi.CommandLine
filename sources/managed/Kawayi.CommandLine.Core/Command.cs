// Copyright (c) 2026 GodotAsync<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Diagnostics.CodeAnalysis;

namespace Kawayi.CommandLine.Core;

public static class Command
{
    public const DynamicallyAccessedMemberTypes CommandMark
        = DynamicallyAccessedMemberTypes.PublicProperties |
          DynamicallyAccessedMemberTypes.Interfaces |
          DynamicallyAccessedMemberTypes.PublicMethods |
          DynamicallyAccessedMemberTypes.PublicParameterlessConstructor;
}
