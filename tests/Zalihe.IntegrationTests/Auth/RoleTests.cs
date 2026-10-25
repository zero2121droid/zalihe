using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Auth;

/// <summary>User roles (SPEC 11.2): who may call what, and how admins change roles.</summary>
[Collection(ApiCollection.Name)]
public class RoleTests(ZaliheApiFactory factory) : IDisposable
{
    /// <summary>Other tests expect cookies checked at the normal pace.</summary>
    public void Dispose() => factory.CookieCheckClock.Offset = TimeSpan.Zero;

    private static async Task<string> RoleOf(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("role").GetString()!;

    private static async Task<Guid> IdOf(HttpClient client) =>
        Guid.Parse((await client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetString()!);

    /// <summary>Lets the time pass after which the sign-in cookie's role is rebuilt from the user.</summary>
    private void PassCookieCheckInterval() => factory.CookieCheckClock.Offset += TimeSpan.FromMinutes(2);

    private static Task<HttpResponseMessage> ChangeRoleAsync(HttpClient admin, Guid userId, string role) =>
        admin.PutAsJsonAsync($"/api/account/users/{userId}/role", new { role });

    /// <summary>Endpoints only admins may call, with a body that would otherwise be accepted or rejected as invalid.</summary>
    public static TheoryData<string, string> AdminOnly => new()
    {
        { "PUT", "/api/account/company" },
        { "GET", "/api/account/users" },
        { "GET", "/api/account/invitations" },
        { "POST", "/api/account/invitations" },
        { "POST", "/api/channels/woocommerce" },
        { "POST", "/api/channels/woocommerce/authorizations" },
        { "PUT", $"/api/account/users/{Guid.NewGuid()}/role" },
        { "PUT", "/api/account/company/purchase-prices" },
    };

    /// <summary>Endpoints for admins and managers.</summary>
    public static TheoryData<string, string> ManagerAndUp => new()
    {
        { "GET", "/api/reports/summary" },
        { "GET", "/api/reports/top-sellers" },
        { "GET", "/api/reports/slow-movers" },
        { "GET", "/api/reports/stock-value" },
        { "POST", "/api/stock/recalculate" },
        { "POST", "/api/imports/items/analyze" },
        { "POST", $"/api/channels/{Guid.NewGuid()}/check" },
        { "GET", $"/api/channels/{Guid.NewGuid()}/products/preview" },
    };

    // The CSV import takes only a form; any other body wouldn't reach it at all.
    private static Task<HttpResponseMessage> CallAsync(HttpClient client, string method, string path) =>
        client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method == "GET" ? null
                : path.StartsWith("/api/imports/", StringComparison.Ordinal) ? new MultipartFormDataContent()
                : JsonContent.Create(new { }),
        });

    [Fact]
    public async Task Register_Owner_IsAdmin()
    {
        // Act
        var owner = await factory.CreateSignedInClientAsync();

        // Assert
        (await RoleOf(owner)).ShouldBe("admin");
    }

    [Theory]
    [InlineData("manager")]
    [InlineData("employee")]
    [InlineData("admin")]
    public async Task Accept_Invitation_GivesTheInvitationsRole(string role)
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();

        // Act
        var invited = await factory.CreateInvitedClientAsync(owner, role);

        // Assert
        (await RoleOf(invited)).ShouldBe(role);
        var users = await owner.GetFromJsonAsync<JsonElement>("/api/account/users");
        users.GetProperty("items").EnumerateArray().Select(u => u.GetProperty("role").GetString()).Order()
            .ShouldBe(new[] { "admin", role }.Order());
    }

    [Fact]
    public async Task Invite_WithoutRole_ReturnsRequired()
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();

        // Act
        var response = await owner.PostAsJsonAsync("/api/account/invitations", new { email = HttpClientExtensions.UniqueEmail() });

        // Assert
        var error = (await response.ReadProblemAsync()).GetProperty("errors")[0];
        error.GetProperty("code").GetString().ShouldBe("validation.required");
        error.GetProperty("field").GetString().ShouldBe("role");
    }

    [Theory]
    [MemberData(nameof(AdminOnly))]
    [MemberData(nameof(ManagerAndUp))]
    public async Task Call_AsEmployee_AdminAndManagerEndpointsAreForbidden(string method, string path)
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        var employee = await factory.CreateInvitedClientAsync(owner, "employee");

        // Act
        var response = await CallAsync(employee, method, path);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ReadProblemAsync()).GetProperty("code").GetString().ShouldBe("auth.forbidden");
    }

    [Theory]
    [MemberData(nameof(AdminOnly))]
    public async Task Call_AsManager_AdminEndpointsAreForbidden(string method, string path)
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        var manager = await factory.CreateInvitedClientAsync(owner, "manager");

        // Act
        var response = await CallAsync(manager, method, path);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(ManagerAndUp))]
    public async Task Call_AsManager_ManagerEndpointsAreAllowed(string method, string path)
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        var manager = await factory.CreateInvitedClientAsync(owner, "manager");

        // Act
        var response = await CallAsync(manager, method, path);

        // Assert: past authorization; an empty body or unknown shop may still be a 400 or 404.
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("GET", "/api/items")]
    [InlineData("GET", "/api/orders")]
    [InlineData("GET", "/api/dashboard")]
    [InlineData("GET", "/api/channels")]
    [InlineData("GET", "/api/stock/summary")]
    public async Task Call_AsEmployee_EverydayWorkIsAllowed(string method, string path)
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        var employee = await factory.CreateInvitedClientAsync(owner, "employee");

        // Act
        var response = await CallAsync(employee, method, path);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Employee_CreatesItemAndRecordsReceipt()
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        var employee = await factory.CreateInvitedClientAsync(owner, "employee");

        // Act
        var created = await employee.PostAsJsonAsync("/api/items", new { name = "Kafa", sku = "KF-1", unit = "kom", minStock = 0 });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        var receipt = await employee.PostAsJsonAsync($"/api/items/{id}/movements", new { kind = "receipt", quantity = 5 });

        // Assert
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        receipt.IsSuccessStatusCode.ShouldBeTrue();
    }

    [Fact]
    public async Task ChangeRole_EmployeeToManager_AppliesWithoutSigningInAgain()
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        var employee = await factory.CreateInvitedClientAsync(owner, "employee");
        (await employee.GetAsync("/api/reports/summary")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Act
        var response = await ChangeRoleAsync(owner, await IdOf(employee), "manager");
        PassCookieCheckInterval();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString().ShouldBe("manager");
        (await employee.GetAsync("/api/reports/summary")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RoleOf(employee)).ShouldBe("manager");
    }

    [Fact]
    public async Task ChangeRole_LastAdmin_IsRefused()
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();

        // Act
        var response = await ChangeRoleAsync(owner, await IdOf(owner), "manager");

        // Assert
        var error = (await response.ReadProblemAsync()).GetProperty("errors")[0];
        error.GetProperty("code").GetString().ShouldBe("user.last_admin");
        error.GetProperty("field").GetString().ShouldBe("role");
        (await RoleOf(owner)).ShouldBe("admin");
    }

    [Fact]
    public async Task ChangeRole_AnotherAdminRemains_OwnerCanStepDown()
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        var admin = await factory.CreateInvitedClientAsync(owner, "admin");

        // Act
        var response = await ChangeRoleAsync(admin, await IdOf(owner), "employee");
        PassCookieCheckInterval();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RoleOf(owner)).ShouldBe("employee");
        (await owner.GetAsync("/api/account/users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangeRole_OtherCompanysUser_ReturnsNotFoundAndChangesNothing()
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        var other = await factory.CreateSignedInClientAsync();
        var otherEmployee = await factory.CreateInvitedClientAsync(other, "employee");

        // Act
        var response = await ChangeRoleAsync(owner, await IdOf(otherEmployee), "admin");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await RoleOf(otherEmployee)).ShouldBe("employee");
    }

    [Fact]
    public async Task ChangeRole_UnknownRole_ReturnsInvalid()
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        var employee = await factory.CreateInvitedClientAsync(owner, "employee");

        // Act
        var response = await owner.PutAsJsonAsync($"/api/account/users/{await IdOf(employee)}/role", new { role = 42 });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await RoleOf(employee)).ShouldBe("employee");
    }
}
