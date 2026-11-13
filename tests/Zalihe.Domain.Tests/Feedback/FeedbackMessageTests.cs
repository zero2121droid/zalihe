using Shouldly;
using Zalihe.Domain.Feedback;

namespace Zalihe.Domain.Tests.Feedback;

public class FeedbackMessageTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Write_Suggestion_IsNewAndTrimmed()
    {
        // Act
        var feedback = FeedbackMessage.Write(Tenant, User, FeedbackKind.Suggestion, "  Show the purchase price.  ", "/items",
            "Mozilla/5.0", mayContact: true, Now);

        // Assert
        feedback.Message.ShouldBe("Show the purchase price.");
        feedback.Status.ShouldBe(FeedbackStatus.New);
        feedback.NeedsReading.ShouldBeTrue();
        feedback.MayContact.ShouldBeTrue();
        feedback.Page.ShouldBe("/items");
        feedback.Prompt.ShouldBeNull();
        feedback.PromptAnswer.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Write_EmptyMessage_Throws(string message)
    {
        Should.Throw<ArgumentException>(() =>
            FeedbackMessage.Write(Tenant, User, FeedbackKind.Problem, message, null, null, false, Now));
    }

    [Fact]
    public void Write_PromptKind_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            FeedbackMessage.Write(Tenant, User, FeedbackKind.Prompt, "Yes", null, null, false, Now));
    }

    [Fact]
    public void Write_TooLongMessage_Throws()
    {
        Should.Throw<ArgumentException>(() => FeedbackMessage.Write(Tenant, User, FeedbackKind.Suggestion,
            new string('a', FeedbackMessage.MessageMaxLength + 1), null, null, false, Now));
    }

    [Fact]
    public void Write_LongPageAndClient_AreCut()
    {
        // Act
        var feedback = FeedbackMessage.Write(Tenant, User, FeedbackKind.Problem, "Broken", new string('p', 500),
            new string('c', 500), false, Now);

        // Assert
        feedback.Page.Length.ShouldBe(FeedbackMessage.PageMaxLength);
        feedback.Client.Length.ShouldBe(FeedbackMessage.ClientMaxLength);
    }

    [Fact]
    public void Answer_PlainYes_IsAlreadyRead()
    {
        // Act
        var answer = FeedbackMessage.Answer(Tenant, User, FeedbackPrompt.CsvImport, positive: true, null, "/items/import", null, Now);

        // Assert
        answer.Kind.ShouldBe(FeedbackKind.Prompt);
        answer.Prompt.ShouldBe(FeedbackPrompt.CsvImport);
        answer.PromptAnswer.ShouldBe(true);
        answer.Message.ShouldBe("");
        answer.Status.ShouldBe(FeedbackStatus.Read);
        answer.NeedsReading.ShouldBeFalse();
    }

    [Fact]
    public void Answer_NotReallyWithoutText_IsNew()
    {
        // Act
        var answer = FeedbackMessage.Answer(Tenant, User, FeedbackPrompt.CsvImport, positive: false, "  ", null, null, Now);

        // Assert
        answer.PromptAnswer.ShouldBe(false);
        answer.Message.ShouldBe("");
        answer.Status.ShouldBe(FeedbackStatus.New);
    }

    [Fact]
    public void Answer_NotReallyWithText_KeepsTheText()
    {
        // Act
        var answer = FeedbackMessage.Answer(Tenant, User, FeedbackPrompt.CsvImport, positive: false,
            "Which column is the minimum?", null, null, Now);

        // Assert
        answer.Message.ShouldBe("Which column is the minimum?");
        answer.NeedsReading.ShouldBeTrue();
    }

    [Fact]
    public void SetStatus_Done_ChangesStatus()
    {
        // Arrange
        var feedback = FeedbackMessage.Write(Tenant, User, FeedbackKind.Suggestion, "Idea", null, null, false, Now);

        // Act
        feedback.SetStatus(FeedbackStatus.Done);

        // Assert
        feedback.Status.ShouldBe(FeedbackStatus.Done);
        feedback.NeedsReading.ShouldBeFalse();
    }
}

public class FeedbackScreenshotTests
{
    private static readonly FeedbackMessage Feedback = FeedbackMessage.Write(Guid.NewGuid(), Guid.NewGuid(), FeedbackKind.Problem,
        "Broken", null, null, false, DateTimeOffset.UnixEpoch);

    [Fact]
    public void New_Png_KeepsTypeAndBelongsToTheMessage()
    {
        // Act
        var screenshot = new FeedbackScreenshot(Feedback, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00]);

        // Assert
        screenshot.ContentType.ShouldBe("image/png");
        screenshot.FeedbackId.ShouldBe(Feedback.Id);
        screenshot.TenantId.ShouldBe(Feedback.TenantId);
    }

    [Fact]
    public void New_NotAPicture_Throws()
    {
        Should.Throw<ArgumentException>(() => new FeedbackScreenshot(Feedback, "<svg/>"u8.ToArray()));
    }

    [Fact]
    public void New_TooLarge_Throws()
    {
        Should.Throw<ArgumentException>(() => new FeedbackScreenshot(Feedback, new byte[FeedbackScreenshot.MaxBytes + 1]));
    }
}
