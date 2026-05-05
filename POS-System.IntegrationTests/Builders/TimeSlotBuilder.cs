using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders;

internal sealed class TimeSlotBuilder
{
    private int _employeeVersionId = 1;
    private DateTime _startTime = DateTime.UtcNow.AddHours(1);
    private bool _isAvailable = true;

    public TimeSlotBuilder WithEmployeeVersionId(int id) { _employeeVersionId = id; return this; }
    public TimeSlotBuilder WithStartTime(DateTime startTime) { _startTime = startTime; return this; }
    public TimeSlotBuilder WithIsAvailable(bool available) { _isAvailable = available; return this; }

    public TimeSlotRequest Build() => new()
    {
        EmployeeVersionId = _employeeVersionId,
        StartTime = _startTime,
        IsAvailable = _isAvailable
    };
}
