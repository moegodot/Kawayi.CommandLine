// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Core.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class ValidatorAttribute : Attribute
{
    public string ValidatorName { get; }

    public ValidatorAttribute(string name)
    {
        ValidatorName = name;
    }
}
