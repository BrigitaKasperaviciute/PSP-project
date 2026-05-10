using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders
{
    /// <summary>
    /// Builder for creating UserRegisterRequest test data.
    /// </summary>
    public class UserRegisterRequestBuilder
    {
        private string _email = $"testuser{Guid.NewGuid()}@example.com";
        private string _userName = $"testuser{Guid.NewGuid()}";
        private string _firstName = "Test";
        private string _lastName = "User";
        private string _password = "TestPassword123!";
        private string _phoneNumber = "1234567890";
        private DateOnly _birthDate = new DateOnly(2000, 1, 1);
        private int _roleId = 0;

        public UserRegisterRequestBuilder WithEmail(string email)
        {
            _email = email;
            return this;
        }

        public UserRegisterRequestBuilder WithUserName(string userName)
        {
            _userName = userName;
            return this;
        }

        public UserRegisterRequestBuilder WithFirstName(string firstName)
        {
            _firstName = firstName;
            return this;
        }

        public UserRegisterRequestBuilder WithLastName(string lastName)
        {
            _lastName = lastName;
            return this;
        }

        public UserRegisterRequestBuilder WithPassword(string password)
        {
            _password = password;
            return this;
        }

        public UserRegisterRequestBuilder WithPhoneNumber(string phoneNumber)
        {
            _phoneNumber = phoneNumber;
            return this;
        }

        public UserRegisterRequestBuilder WithBirthDate(DateOnly birthDate)
        {
            _birthDate = birthDate;
            return this;
        }

        public UserRegisterRequestBuilder WithRoleId(int roleId)
        {
            _roleId = roleId;
            return this;
        }

        public UserRegisterRequest Build()
        {
            return new UserRegisterRequest(
                _email,
                _userName,
                _firstName,
                _lastName,
                _password,
                _phoneNumber,
                _birthDate,
                _roleId
            );
        }

        public static UserRegisterRequest CreateDefault()
        {
            return new UserRegisterRequestBuilder().Build();
        }

        public static UserRegisterRequest CreateWithUserName(string userName)
        {
            return new UserRegisterRequestBuilder().WithUserName(userName).Build();
        }

        public static UserRegisterRequest CreateWithEmail(string email)
        {
            return new UserRegisterRequestBuilder().WithEmail(email).Build();
        }
    }
}
