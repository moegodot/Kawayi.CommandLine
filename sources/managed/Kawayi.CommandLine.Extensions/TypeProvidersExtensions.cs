// Copyright (c) 2026 GodotAsync<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Extensions;

/// <summary>
/// Extensions for <see cref="TypeProviders"/>
/// </summary>
public static class TypeProvidersExtensions
{
    extension(TypeProviders providers)
    {
        /// <summary>
        /// Attempts to merge two <see cref="TypeProviders"/>
        /// </summary>
        /// <param name="another">Those providers are appended after the current providers.</param>
        /// <param name="allowOverride">Whether conflicting providers from <paramref name="another"/> replace current providers.</param>
        /// <param name="result">The merged providers when the merge succeeds.</param>
        /// <returns><see langword="true"/> when the providers were merged; otherwise, <see langword="false"/>.</returns>
        public bool TryMerge(TypeProviders another, bool allowOverride, [NotNullWhen(true)] out TypeProviders? result)
        {
            if (TryMergeCore(providers, another, allowOverride, out var merged, out _))
            {
                result = merged.Value;
                return true;
            }

            result = null;
            return false;
        }

        /// <summary>
        /// Attempts to merge two <see cref="TypeProviders"/>
        /// </summary>
        /// <param name="another">The providers are appended after the current providers.</param>
        /// <param name="allowOverride">Whether conflicting providers from <paramref name="another"/> replace current providers.</param>
        /// <returns>The merged providers.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the providers conflict and overriding is disabled.</exception>
        public TypeProviders Merge(TypeProviders another, bool allowOverride = false)
        {
            if (TryMergeCore(providers, another, allowOverride, out var merged, out var reason))
            {
                return merged.Value;
            }

            throw new InvalidOperationException(reason);
        }
    }

    private static bool TryMergeCore(
        TypeProviders first,
        TypeProviders second,
        bool allowOverride,
        [NotNullWhen(true)] out TypeProviders? result,
        [NotNullWhen(false)] out string? error)
    {
        result = null;
        error = null;

        var basics = ImmutableDictionary.CreateBuilder<Type, ITypeProvider>();
        basics.AddRange(first.Providers);
        var extensions = ImmutableArray.CreateBuilder<IExtendedTypeProvider>();
        extensions.AddRange(first.ExtendedProviders);
        extensions.AddRange(second.ExtendedProviders);

        foreach (var provider in second.Providers)
        {
            if (allowOverride)
            {
                basics[provider.Key] = provider.Value;
            }
            else
            {
                if(basics.TryGetValue(provider.Key,out var _))
                {
                    error = $"provider of type {provider.Key} was defined more than once and overriding is disabled";
                    return false;
                }
                basics.Add(provider.Key, provider.Value);
            }
        }

        result = new TypeProviders(
            basics.ToImmutable(),
            extensions.ToImmutable()
            );
        return true;
    }
}
