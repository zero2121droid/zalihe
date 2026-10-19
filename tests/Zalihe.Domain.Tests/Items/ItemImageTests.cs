using System.Text;
using Shouldly;
using Zalihe.Domain.Items;

namespace Zalihe.Domain.Tests.Items;

public class ItemImageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);

    private static Item Item() => new(Guid.NewGuid(), "Kafa Etiopija 250 g", "KF-ETI-250", Unit.Kom, 0, Now);

    public static byte[] Jpeg => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, .. "JFIF"u8];
    public static byte[] Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
    public static byte[] WebP => [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WEBPVP8 "u8];

    public static TheoryData<byte[], string> Pictures => new()
    {
        { Jpeg, "image/jpeg" },
        { Png, "image/png" },
        { WebP, "image/webp" },
    };

    [Theory]
    [MemberData(nameof(Pictures))]
    public void Constructor_Picture_KeepsItWithTypeFromItsContent(byte[] data, string contentType)
    {
        // Arrange
        var item = Item();

        // Act
        var image = new ItemImage(item, data, Now);

        // Assert
        image.ItemId.ShouldBe(item.Id);
        image.TenantId.ShouldBe(item.TenantId);
        image.ContentType.ShouldBe(contentType);
        image.Data.ShouldBe(data);
        image.UpdatedAt.ShouldBe(Now);
    }

    public static TheoryData<byte[]> NotPictures => new()
    {
        Array.Empty<byte>(),
        Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"),
        Encoding.UTF8.GetBytes("GIF89a"),
        Encoding.UTF8.GetBytes("RIFF1234WAVE"),
        Encoding.UTF8.GetBytes("%PDF-1.7"),
    };

    [Theory]
    [MemberData(nameof(NotPictures))]
    public void Constructor_NotAJpegPngOrWebP_Throws(byte[] data)
    {
        Should.Throw<ArgumentException>(() => new ItemImage(Item(), data, Now));
    }

    [Fact]
    public void Constructor_LargerThanMax_Throws()
    {
        byte[] data = [.. Jpeg, .. new byte[ItemImage.MaxBytes]];

        Should.Throw<ArgumentOutOfRangeException>(() => new ItemImage(Item(), data, Now));
    }

    [Fact]
    public void Replace_NewPicture_ChangesTypeAndTime()
    {
        // Arrange
        var image = new ItemImage(Item(), Jpeg, Now);

        // Act
        image.Replace(Png, Now.AddDays(1));

        // Assert
        image.ContentType.ShouldBe("image/png");
        image.Data.ShouldBe(Png);
        image.UpdatedAt.ShouldBe(Now.AddDays(1));
    }

    [Fact]
    public void Replace_NotAPicture_KeepsTheOldOne()
    {
        // Arrange
        var image = new ItemImage(Item(), Jpeg, Now);

        // Act
        Should.Throw<ArgumentException>(() => image.Replace("nije slika"u8.ToArray(), Now.AddDays(1)));

        // Assert
        image.ContentType.ShouldBe("image/jpeg");
        image.UpdatedAt.ShouldBe(Now);
    }
}
