// Copyright (c) 2026 GodotAsync<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

namespace Project.Maintainer;

public static class Out
{
    public const string StartColor = "\e[32;45m";
    public const string StopColor = "\e[0m";

    public static void OutColor(string msg)
    {
        Console.WriteLine($"{StartColor}{msg}{StopColor}");
    }
}
