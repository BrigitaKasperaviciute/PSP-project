namespace POS_System.Common.Constants
{
    public class ApplicationConstants
    {
        public const int MINIMUM_AMOUNT_PARTIAL_PAYMENT = 3000;
        public const int MAXIMUM_SPLIT_CASHOUT_COUNT = 5;
        // Redirect to Swagger UI root in tests so the test server returns a 200/302 instead of an external host
        public const string REDIRECT_URL = "/swagger/index.html";
        public const string PLACE_HOLDER_IMAGE_URL = "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcROqNClZI6I_euxH1lEPcqhgIxHIUeIFpSHJg&s";
    }
}