using RomaERP.API.Services;
using RomaERP.Application.Common.Exceptions;
using Xunit;

namespace RomaERP.UnitTests;

public class UploadSafetyTests
{
    [Theory]
    [InlineData("receipt.JPG", ".jpg")]
    [InlineData("scan.pdf", ".pdf")]
    [InlineData("photo.webp", ".webp")]
    public void ProofExtension_AcceptsOnlyKnownTypes(string name, string expected)
        => Assert.Equal(expected, UploadSafety.ProofExtension(name));

    [Theory]
    [InlineData("x.html")]
    [InlineData("x.exe")]
    [InlineData("x")]
    [InlineData("x.jpg.aspx")]
    [InlineData("..\\..\\evil.dll")]
    [InlineData("a.\\..\\..\\y")]
    [InlineData(null)]
    public void ProofExtension_RejectsEverythingElse(string? name)
        => Assert.Throws<ValidationAppException>(() => UploadSafety.ProofExtension(name));

    [Fact]
    public void Signatures_AreRecognised()
    {
        Assert.True(UploadSafety.LooksLikeJpeg(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));
        Assert.False(UploadSafety.LooksLikeJpeg(new byte[] { 0x89, 0x50, 0x4E }));
        Assert.True(UploadSafety.LooksLikePdf("%PDF-1.7"u8.ToArray()));
        Assert.False(UploadSafety.LooksLikePdf("MZ\0\0\0"u8.ToArray()));
    }
}
