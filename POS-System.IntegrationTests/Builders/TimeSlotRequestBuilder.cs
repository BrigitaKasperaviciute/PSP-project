using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders
{
    public class TimeSlotRequestBuilder
    {
        private int _employeeVersionId = 1;
        private DateTime _startTime = DateTime.UtcNow.AddHours(1);
        private bool _isAvailable = true;

        public TimeSlotRequestBuilder WithEmployeeVersionId(int employeeVersionId)
        {
            _employeeVersionId = employeeVersionId;
            return this;
        }

        public TimeSlotRequestBuilder WithStartTime(DateTime startTime)
        {
            _startTime = startTime;
            return this;
        }

        public TimeSlotRequestBuilder WithIsAvailable(bool isAvailable)
        {
            _isAvailable = isAvailable;
            return this;
        }

        public TimeSlotRequest Build()
        {
            return new TimeSlotRequest
            {
                EmployeeVersionId = _employeeVersionId,
                StartTime = _startTime,
                IsAvailable = _isAvailable
            };
        }

        public static TimeSlotRequest CreateDefault()
        {
            return new TimeSlotRequestBuilder().Build();
        }
    }
}