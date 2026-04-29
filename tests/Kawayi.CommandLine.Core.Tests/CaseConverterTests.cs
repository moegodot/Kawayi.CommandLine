// Copyright (c) 2026 MoeGodot<me@kawayi.moe>.
// Licensed under the GNU Affero General Public License v3-or-later license.

using Kawayi.CommandLine.Abstractions;

namespace Kawayi.CommandLine.Core.Tests;

public class CaseConverterTests
{
    [Test]
    public async Task Pascal2Kebab_Converts_Common_Identifier_Shapes()
    {
        await Assert.That(CaseConverter.Pascal2Kebab("RequestId")).IsEqualTo("request-id");
        await Assert.That(CaseConverter.Pascal2Kebab("executionMode")).IsEqualTo("execution-mode");
        await Assert.That(CaseConverter.Pascal2Kebab("HTTPServer2URL")).IsEqualTo("http-server-2-url");
        await Assert.That(CaseConverter.Pascal2Kebab("request_id value")).IsEqualTo("request-id-value");
        await Assert.That(CaseConverter.Pascal2Kebab("already-kebab")).IsEqualTo("already-kebab");
        await Assert.That(CaseConverter.Pascal2Kebab(string.Empty)).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Pascal2Kebab_Throws_For_Null()
    {
        await Assert.That(() => CaseConverter.Pascal2Kebab(null!)).Throws<ArgumentNullException>();
    }
}
