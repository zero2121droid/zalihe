using Shouldly;
using Zalihe.Domain.Users;

namespace Zalihe.Domain.Tests.Users;

public class PendingRegistrationTests
{
    private const string Token = "tajni-token";
    private const string Code = "123456";
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private static PendingRegistration Pending() =>
        new("  Ana@Zrno.RS ", "  Zrno  ", " Ana ", Languages.Default, "hes-lozinke", Token, Code, Now);

    private static void Replace(PendingRegistration pending, string token, string code, DateTimeOffset at) =>
        pending.Replace("Nova firma", "Marko", Languages.English, "drugi-hes", token, code, at);

    [Fact]
    public void Constructor_Details_AreNormalizedAndSecretsKeptOnlyHashed()
    {
        // Act
        var pending = Pending();

        // Assert
        pending.Email.ShouldBe("ana@zrno.rs");
        pending.CompanyName.ShouldBe("Zrno");
        pending.Name.ShouldBe("Ana");
        pending.TokenHash.ShouldBe(PendingRegistration.HashToken(Token));
        pending.CodeHash.ShouldNotContain(Code);
        pending.CodesSent.ShouldBe(1);
    }

    [Fact]
    public void NewCode_Always_HasSixDigits()
    {
        // Act
        var codes = Enumerable.Range(0, 1000).Select(_ => PendingRegistration.NewCode()).ToList();

        // Assert
        codes.ShouldAllBe(code => code.Length == PendingRegistration.CodeLength && code.All(char.IsAsciiDigit));
    }

    [Fact]
    public void CheckCode_CorrectJustBeforeExpiry_IsCorrect()
    {
        // Act
        var result = Pending().CheckCode(Code, Now + PendingRegistration.CodeValidFor - TimeSpan.FromSeconds(1));

        // Assert
        result.ShouldBe(CodeCheck.Correct);
    }

    [Fact]
    public void CheckCode_AfterExpiry_IsExpiredEvenWhenCorrect()
    {
        // Act
        var result = Pending().CheckCode(Code, Now + PendingRegistration.CodeValidFor);

        // Assert
        result.ShouldBe(CodeCheck.Expired);
    }

    [Fact]
    public void CheckCode_Wrong_IsCounted()
    {
        // Arrange
        var pending = Pending();

        // Act
        var result = pending.CheckCode("000000", Now);

        // Assert
        result.ShouldBe(CodeCheck.Wrong);
        pending.FailedAttempts.ShouldBe(1);
    }

    [Fact]
    public void CheckCode_LastAttemptWrong_UsesTheCodeUpSoTheCorrectOneNoLongerWorks()
    {
        // Arrange
        var pending = Pending();
        for (var attempt = 1; attempt < PendingRegistration.MaxAttempts; attempt++)
        {
            pending.CheckCode("000000", Now).ShouldBe(CodeCheck.Wrong);
        }

        // Act
        var last = pending.CheckCode("000000", Now);
        var afterwards = pending.CheckCode(Code, Now);

        // Assert
        last.ShouldBe(CodeCheck.NoAttemptsLeft);
        afterwards.ShouldBe(CodeCheck.NoAttemptsLeft);
    }

    [Fact]
    public void Resend_TooSoon_Throws()
    {
        // Arrange
        var pending = Pending();

        // Act & Assert
        pending.CanResendAt(Now + PendingRegistration.ResendAfter - TimeSpan.FromSeconds(1)).ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => pending.Resend("654321", Now));
    }

    [Fact]
    public void Resend_AfterWaiting_NewCodeWorksOldDoesNotAndAttemptsStartOver()
    {
        // Arrange
        var pending = Pending();
        pending.CheckCode("000000", Now);
        var later = Now + PendingRegistration.ResendAfter;

        // Act
        pending.Resend("654321", later);

        // Assert
        pending.FailedAttempts.ShouldBe(0);
        pending.CodesSent.ShouldBe(2);
        pending.CheckCode(Code, later).ShouldBe(CodeCheck.Wrong);
        pending.CheckCode("654321", later).ShouldBe(CodeCheck.Correct);
    }

    [Fact]
    public void Replace_SameAddressAgain_NewDetailsAndTokenAndTheCountCarriesOn()
    {
        // Arrange
        var pending = Pending();

        // Act
        Replace(pending, "drugi-token", "654321", Now);

        // Assert
        pending.CompanyName.ShouldBe("Nova firma");
        pending.PasswordHash.ShouldBe("drugi-hes");
        pending.TokenHash.ShouldBe(PendingRegistration.HashToken("drugi-token"));
        pending.CodesSent.ShouldBe(2);
        pending.CheckCode(Code, Now).ShouldBe(CodeCheck.Wrong);
    }

    [Fact]
    public void Replace_LimitOfCodesReached_Throws()
    {
        // Arrange
        var pending = Pending();
        for (var sent = 1; sent < PendingRegistration.MaxCodes; sent++)
        {
            Replace(pending, Token, Code, Now);
        }

        // Act & Assert
        pending.CodeLimitReachedAt(Now).ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => Replace(pending, Token, Code, Now));
    }

    [Fact]
    public void Replace_AfterTheDayIsOver_TheCountStartsOver()
    {
        // Arrange
        var pending = Pending();
        for (var sent = 1; sent < PendingRegistration.MaxCodes; sent++)
        {
            Replace(pending, Token, Code, Now);
        }

        var nextDay = Now + PendingRegistration.KeptFor;

        // Act
        Replace(pending, Token, Code, nextDay);

        // Assert
        pending.CodesSent.ShouldBe(1);
        pending.StartedAt.ShouldBe(nextDay);
        pending.IsAbandonedAt(nextDay).ShouldBeFalse();
    }
}
