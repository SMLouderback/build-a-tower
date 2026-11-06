using Playtest.Api;

namespace Playtest.Api.Tests;

public sealed class EmailValidationTests
{
    [Theory]
    [InlineData("v.a.le.nc.ia.o.p.r.a.pl@gmail.com", "valenciaoprapl@gmail.com")]
    [InlineData("va.le.n.c.ia.opra.pl@gmail.com", "valenciaoprapl@gmail.com")]
    [InlineData("Valencia+spam@Googlemail.com", "valencia@gmail.com")]
    [InlineData("friend@example.com", "friend@example.com")]
    public void Canonicalize_collapses_gmail_aliases(string raw, string expectedCanonical)
    {
        Assert.True(EmailValidation.TryNormalizeAndCanonicalize(raw, out _, out var canonical));
        Assert.Equal(expectedCanonical, canonical);
    }
}
