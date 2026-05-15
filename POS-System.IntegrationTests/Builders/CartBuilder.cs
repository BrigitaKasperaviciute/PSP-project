using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders;

public sealed class CartBuilder
{
    private int _employeeVersionId = 1;

    public CartBuilder WithEmployeeVersionId(int id) { _employeeVersionId = id; return this; }

    public CartRequest Build() => new() { EmployeeVersionId = _employeeVersionId };
}
