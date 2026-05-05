using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders;

internal sealed class ServiceBuilder
{
    private string _name = $"Service-{Guid.NewGuid():N}";
    private string _description = "Test service description";
    private int _duration = 30;
    private int _price = 2000;
    private string _imageUrl = "https://example.com/service.jpg";
    private int _employeeId = 1;

    public ServiceBuilder WithName(string name) { _name = name; return this; }
    public ServiceBuilder WithPrice(int price) { _price = price; return this; }
    public ServiceBuilder WithEmployeeId(int employeeId) { _employeeId = employeeId; return this; }

    public ServiceRequest Build() => new()
    {
        Name = _name,
        Description = _description,
        Duration = _duration,
        Price = _price,
        ImageURL = _imageUrl,
        EmployeeId = _employeeId
    };
}
