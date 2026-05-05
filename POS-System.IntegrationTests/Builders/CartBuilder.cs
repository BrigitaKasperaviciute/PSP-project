using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders;

internal sealed class CartBuilder
{
    // Employee ID 1 always exists (seeded in migrations).
    private int _employeeVersionId = 1;

    public CartBuilder WithEmployeeVersionId(int id) { _employeeVersionId = id; return this; }

    public CartRequest Build() => new()
    {
        EmployeeVersionId = _employeeVersionId
    };
}
