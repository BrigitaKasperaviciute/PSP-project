namespace POS_System.Business.Dtos.Response;

public record GiftCardResponse
{
    public string Id { get; init; } = string.Empty;
    public DateOnly Date { get; init; }
    public int Value { get; init; }
}
