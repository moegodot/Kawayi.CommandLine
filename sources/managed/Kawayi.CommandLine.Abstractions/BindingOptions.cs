// Copyright (c) 2026 GodotAsync<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.Abstractions;

/// <summary>
/// options for <see cref="IBindable.Bind"/>
/// </summary>
/// <param name="CheckGeneratedType">true to check current type matches <see cref="CliSchema.GeneratedFrom"/></param>
public record BindingOptions(bool CheckGeneratedType = true);
