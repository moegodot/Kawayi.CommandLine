// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Abstractions.Tests;

public class ValueRangeTests
{
    [Test]
    public async Task Constructor_Stores_Minimum_And_Maximum()
    {
        var range = new ValueRange(2, 5);

        await Assert.That(range.Minimum).IsEqualTo(2);
        await Assert.That(range.Maximum).IsEqualTo(5);
    }

    [Test]
    public async Task Constructor_Throws_When_Minimum_Is_Negative()
    {
        await Assert.That(() => new ValueRange(-1, 1)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Constructor_Throws_When_Maximum_Is_Negative()
    {
        await Assert.That(() => new ValueRange(0, -1)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Constructor_Throws_When_Minimum_Is_Greater_Than_Maximum()
    {
        await Assert.That(() => new ValueRange(2, 1)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Preset_Ranges_Match_Expected_Bounds()
    {
        await Assert.That(ValueRange.ZeroOrOne).IsEqualTo(new ValueRange(0, 1));
        await Assert.That(ValueRange.ZeroOrMore).IsEqualTo(new ValueRange(0, int.MaxValue));
        await Assert.That(ValueRange.One).IsEqualTo(new ValueRange(1, 1));
        await Assert.That(ValueRange.OneOrMore).IsEqualTo(new ValueRange(1, int.MaxValue));
    }
}
