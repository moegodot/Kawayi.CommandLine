// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// Defines a type that can populate itself from parsing results.
/// </summary>
public interface IBindable
{
    /// <summary>
    /// Populates the current instance from parsing results.
    /// </summary>
    /// <param name="results">The parsed results to bind from.</param>
    /// <param name="bindingOptions">options for process binding</param>
    void Bind(Cli results,BindingOptions bindingOptions);
}
