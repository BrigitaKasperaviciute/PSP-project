namespace POS_System.Business.Dtos.Request
{
    public record CartRequest
    {
        public int EmployeeVersionId { get; set; }
        // Added to support integration tests which set cart status
        public POS_System.Common.Enums.CartStatusEnum Status { get; set; } = POS_System.Common.Enums.CartStatusEnum.PENDING;
    }
}
