using Shouldly;
using Zalihe.Domain.Users;

namespace Zalihe.Domain.Tests.Users;

public class InvitationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.NewGuid();

    private static Invitation Invite(string token = "tajni-token") => new(Guid.NewGuid(), "  Ana@Zrno.RS ", token, Owner, Now);

    [Fact]
    public void Constructor_Email_IsNormalizedAndOnlyTheTokensHashIsKept()
    {
        // Act
        var invitation = Invite("tajni-token");

        // Assert
        invitation.Email.ShouldBe("ana@zrno.rs");
        invitation.TokenHash.ShouldBe(Invitation.HashToken("tajni-token"));
        invitation.TokenHash.ShouldNotContain("tajni-token");
        invitation.ExpiresAt.ShouldBe(Now + Invitation.ValidFor);
        invitation.StateAt(Now).ShouldBe(InvitationState.Pending);
    }

    [Fact]
    public void StateAt_AfterAWeek_IsExpired()
    {
        var invitation = Invite();

        invitation.StateAt(Now + Invitation.ValidFor - TimeSpan.FromSeconds(1)).ShouldBe(InvitationState.Pending);
        invitation.StateAt(Now + Invitation.ValidFor).ShouldBe(InvitationState.Expired);
    }

    [Fact]
    public void Accept_Pending_IsAcceptedAndCantBeUsedAgain()
    {
        // Arrange
        var invitation = Invite();

        // Act
        invitation.Accept(Now.AddHours(1));

        // Assert
        invitation.StateAt(Now.AddHours(2)).ShouldBe(InvitationState.Accepted);
        Should.Throw<InvalidOperationException>(() => invitation.Accept(Now.AddHours(2)));
    }

    [Fact]
    public void Accept_Expired_Throws()
    {
        var invitation = Invite();

        Should.Throw<InvalidOperationException>(() => invitation.Accept(Now + Invitation.ValidFor));
    }

    [Fact]
    public void Revoke_Pending_StopsItFromBeingAccepted()
    {
        // Arrange
        var invitation = Invite();

        // Act
        invitation.Revoke(Now.AddHours(1));

        // Assert
        invitation.StateAt(Now.AddHours(1)).ShouldBe(InvitationState.Revoked);
        Should.Throw<InvalidOperationException>(() => invitation.Accept(Now.AddHours(2)));
    }

    [Fact]
    public void Renew_ExpiredOrRevoked_GivesANewTokenAndAFreshWeek()
    {
        // Arrange
        var invitation = Invite("stari-token");
        invitation.Revoke(Now.AddDays(1));
        var later = Now.AddDays(10);

        // Act
        invitation.Renew("novi-token", Owner, later);

        // Assert
        invitation.TokenHash.ShouldBe(Invitation.HashToken("novi-token"));
        invitation.SentAt.ShouldBe(later);
        invitation.ExpiresAt.ShouldBe(later + Invitation.ValidFor);
        invitation.StateAt(later).ShouldBe(InvitationState.Pending);
    }

    [Fact]
    public void Renew_Accepted_Throws()
    {
        var invitation = Invite();
        invitation.Accept(Now.AddHours(1));

        Should.Throw<InvalidOperationException>(() => invitation.Renew("novi-token", Owner, Now.AddDays(1)));
    }

    [Fact]
    public void NewToken_EachCall_IsLongUrlSafeAndDifferent()
    {
        var first = Invitation.NewToken();
        var second = Invitation.NewToken();

        first.ShouldNotBe(second);
        first.Length.ShouldBe(43);
        first.ShouldMatch("^[A-Za-z0-9_-]+$");
    }
}
