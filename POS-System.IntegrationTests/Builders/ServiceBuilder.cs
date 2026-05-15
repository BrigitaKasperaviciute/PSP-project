using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders;

public sealed class ServiceBuilder
{
    private string _name = $"Service_{Guid.NewGuid().ToString("N")[..8]}";
    private string _description = "Test service description";
    private int _duration = 60;
    private int _price = 1000;
    private string _imageUrl = "http://example.com/image.jpg";
    private int _employeeId = 1;

    public ServiceBuilder WithName(string name) { _name = name; return this; }
    public ServiceBuilder WithPrice(int price) { _price = price; return this; }
    public ServiceBuilder WithDuration(int duration) { _duration = duration; return this; }
    public ServiceBuilder WithEmployeeId(int id) { _employeeId = id; return this; }

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
