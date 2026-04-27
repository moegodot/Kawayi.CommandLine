// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using System.Reflection;
using Kawayi.CommandLine.ExitCodes;

namespace Kawayi.CommandLine.ExitCodes.Tests;

public class ExitCodeTests
{
    private static readonly (string Name, int Expected)[] ConstantCases =
    [
        (nameof(ExitCode.Success), 0),
        (nameof(ExitCode.Failure), 1),
        (nameof(ExitCode.UsageError), 64),
        (nameof(ExitCode.DataError), 65),
        (nameof(ExitCode.NoInput), 66),
        (nameof(ExitCode.NoUser), 67),
        (nameof(ExitCode.NoHost), 68),
        (nameof(ExitCode.ServiceUnavailable), 69),
        (nameof(ExitCode.SoftwareError), 70),
        (nameof(ExitCode.OsError), 71),
        (nameof(ExitCode.OsFileError), 72),
        (nameof(ExitCode.CannotCreate), 73),
        (nameof(ExitCode.IoError), 74),
        (nameof(ExitCode.TemporaryFailure), 75),
        (nameof(ExitCode.ProtocolError), 76),
        (nameof(ExitCode.NoPermission), 77),
        (nameof(ExitCode.ConfigError), 78),
    ];

    [Test]
    public async Task Constants_Match_The_Rust_Crate()
    {
        foreach (var (name, expected) in ConstantCases)
        {
            var field = GetPublicStaticField(name);
            var actual = (int)field.GetRawConstantValue()!;

            await Assert.That(actual).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task ExitCode_Exposes_Public_Int_Constants_On_A_Static_Class()
    {
        var exitCodeType = typeof(ExitCode);

        await Assert.That(exitCodeType.IsAbstract).IsTrue();
        await Assert.That(exitCodeType.IsSealed).IsTrue();

        foreach (var (name, _) in ConstantCases)
        {
            var field = GetPublicStaticField(name);

            await Assert.That(field.IsPublic).IsTrue();
            await Assert.That(field.IsStatic).IsTrue();
            await Assert.That(field.IsLiteral).IsTrue();
            await Assert.That(field.FieldType).IsEqualTo(typeof(int));
        }
    }

    [Test]
    public async Task IsValid_Matches_The_Crate_Range_Check()
    {
        await Assert.That(ExitCode.IsValid(-1)).IsFalse();
        await Assert.That(ExitCode.IsValid(0)).IsTrue();
        await Assert.That(ExitCode.IsValid(1)).IsTrue();
        await Assert.That(ExitCode.IsValid(64)).IsTrue();
        await Assert.That(ExitCode.IsValid(78)).IsTrue();
        await Assert.That(ExitCode.IsValid(126)).IsTrue();
        await Assert.That(ExitCode.IsValid(137)).IsTrue();
        await Assert.That(ExitCode.IsValid(255)).IsTrue();
        await Assert.That(ExitCode.IsValid(256)).IsFalse();
        await Assert.That(ExitCode.IsValid(int.MaxValue)).IsFalse();
    }

    [Test]
    public async Task IsReserved_Matches_The_Crate_Reserved_Ranges()
    {
        await Assert.That(ExitCode.IsReserved(-1)).IsFalse();
        await Assert.That(ExitCode.IsReserved(0)).IsTrue();
        await Assert.That(ExitCode.IsReserved(1)).IsTrue();
        await Assert.That(ExitCode.IsReserved(2)).IsTrue();
        await Assert.That(ExitCode.IsReserved(3)).IsFalse();
        await Assert.That(ExitCode.IsReserved(63)).IsFalse();
        await Assert.That(ExitCode.IsReserved(64)).IsTrue();
        await Assert.That(ExitCode.IsReserved(78)).IsTrue();
        await Assert.That(ExitCode.IsReserved(79)).IsFalse();
        await Assert.That(ExitCode.IsReserved(125)).IsFalse();
        await Assert.That(ExitCode.IsReserved(126)).IsTrue();
        await Assert.That(ExitCode.IsReserved(128)).IsTrue();
        await Assert.That(ExitCode.IsReserved(137)).IsTrue();
        await Assert.That(ExitCode.IsReserved(138)).IsFalse();
        await Assert.That(ExitCode.IsReserved(255)).IsFalse();
        await Assert.That(ExitCode.IsReserved(256)).IsFalse();
    }

    private static FieldInfo GetPublicStaticField(string name)
    {
        return typeof(ExitCode).GetField(name, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Missing public static field '{name}'.");
    }
}
