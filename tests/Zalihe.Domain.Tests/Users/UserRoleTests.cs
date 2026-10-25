using Shouldly;
using Zalihe.Domain.Users;

namespace Zalihe.Domain.Tests.Users;

public class UserRoleTests
{
    [Theory]
    [InlineData(UserRole.Admin, false, true)]
    [InlineData(UserRole.Manager, false, true)]
    [InlineData(UserRole.Employee, false, false)]
    [InlineData(UserRole.Employee, true, true)]
    public void SeesPurchasePrices_ByRoleAndSetting_OnlyEmployeesDependOnIt(UserRole role, bool employeesSee, bool expected)
    {
        role.SeesPurchasePrices(employeesSee).ShouldBe(expected);
    }
}
