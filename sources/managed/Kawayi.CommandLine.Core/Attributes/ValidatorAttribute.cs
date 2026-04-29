// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Core.Attributes;

/// <summary>
/// Declares a validation method for an argument or property.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class ValidatorAttribute : Attribute
{
    /// <summary>
    /// Gets the validator method name.
    /// </summary>
    public string ValidatorName { get; }

    /// <summary>
    /// Initializes a new validator attribute.
    /// </summary>
    /// <param name="name">The validator method name.</param>
    public ValidatorAttribute(string name)
    {
        ValidatorName = name;
    }
}
