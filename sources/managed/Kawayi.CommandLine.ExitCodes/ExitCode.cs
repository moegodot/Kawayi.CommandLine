// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Kawayi.CommandLine.ExitCodes;

/// <summary>
/// Common process exit codes derived from <c>sysexits</c> and shell conventions.
/// </summary>
public static class ExitCode
{
    /// <summary>
    /// Successful termination.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// Unsuccessful termination when no more specific exit code applies.
    /// </summary>
    public const int Failure = 1;

    /// <summary>
    /// The command was used incorrectly.
    /// </summary>
    public const int UsageError = 64;

    /// <summary>
    /// The input data was incorrect in some way.
    /// </summary>
    public const int DataError = 65;

    /// <summary>
    /// An input file did not exist or was not readable.
    /// </summary>
    public const int NoInput = 66;

    /// <summary>
    /// The specified user did not exist.
    /// </summary>
    public const int NoUser = 67;

    /// <summary>
    /// The specified host did not exist.
    /// </summary>
    public const int NoHost = 68;

    /// <summary>
    /// A service is unavailable.
    /// </summary>
    public const int ServiceUnavailable = 69;

    /// <summary>
    /// An internal software error has been detected.
    /// </summary>
    public const int SoftwareError = 70;

    /// <summary>
    /// An operating system error has been detected.
    /// </summary>
    public const int OsError = 71;

    /// <summary>
    /// A system file could not be opened or was invalid.
    /// </summary>
    public const int OsFileError = 72;

    /// <summary>
    /// A requested output file could not be created.
    /// </summary>
    public const int CannotCreate = 73;

    /// <summary>
    /// An I/O error occurred while working with a file.
    /// </summary>
    public const int IoError = 74;

    /// <summary>
    /// A temporary failure occurred and the operation may succeed later.
    /// </summary>
    public const int TemporaryFailure = 75;

    /// <summary>
    /// A remote system returned a protocol-level impossibility.
    /// </summary>
    public const int ProtocolError = 76;

    /// <summary>
    /// Higher-level permissions were insufficient to perform the operation.
    /// </summary>
    public const int NoPermission = 77;

    /// <summary>
    /// Something was found in an unconfigured or misconfigured state.
    /// </summary>
    public const int ConfigError = 78;

    /// <summary>
    /// Checks whether the exit code is reserved by shells or standard process conventions.
    /// </summary>
    /// <param name="exitCode">The exit code to inspect.</param>
    /// <returns><see langword="true"/> when the exit code has a reserved meaning; otherwise <see langword="false"/>.</returns>
    public static bool IsReserved(int exitCode)
    {
        return (0 <= exitCode && exitCode <= 2)
            || (64 <= exitCode && exitCode <= 78)
            || (126 <= exitCode && exitCode <= 137);
    }

    /// <summary>
    /// Checks whether the exit code is representable as a process exit status.
    /// </summary>
    /// <param name="exitCode">The exit code to inspect.</param>
    /// <returns><see langword="true"/> when the exit code is in the inclusive range <c>0..255</c>; otherwise <see langword="false"/>.</returns>
    public static bool IsValid(int exitCode)
    {
        return 0 <= exitCode && exitCode <= 255;
    }
}
