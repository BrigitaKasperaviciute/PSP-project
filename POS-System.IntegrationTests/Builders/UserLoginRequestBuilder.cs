using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders
{
    /// <summary>
    /// Builder for creating UserLoginRequest test data.
    /// </summary>
    public class UserLoginRequestBuilder
    {
        private string _userName = "testuser";
        private string _password = "TestPassword123!";

        public UserLoginRequestBuilder WithUserName(string userName)
        {
            _userName = userName;
            return this;
        }

        public UserLoginRequestBuilder WithPassword(string password)
        {
            _password = password;
            return this;
        }

        public UserLoginRequest Build()
        {
            return new UserLoginRequest(_userName, _password);
        }

        public static UserLoginRequest CreateDefault()
        {
            return new UserLoginRequestBuilder().Build();
        }

        public static UserLoginRequest CreateWithUserName(string userName)
        {
            return new UserLoginRequestBuilder().WithUserName(userName).Build();
        }
    }
}
