using System.ComponentModel.DataAnnotations;
using Zalihe.Domain.Feedback;

namespace Zalihe.Web.Feedback;

/// <param name="Positive">"Yes" (true) or "not really" (false).</param>
/// <param name="Message">What wasn't clear; optional.</param>
public record AnswerPromptRequest(
    [Required(ErrorMessage = "validation.required")]
    bool? Positive,
    [MaxLength(FeedbackMessage.MessageMaxLength, ErrorMessage = "validation.max_length")]
    string? Message,
    [MaxLength(FeedbackMessage.PageMaxLength, ErrorMessage = "validation.max_length")]
    string? Page);

public record SetFeedbackStatusRequest(
    [Required(ErrorMessage = "validation.required")]
    FeedbackStatus? Status);
