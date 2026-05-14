using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS_System.Domain.Entities
{
    [Table("CartDiscounts")]
    public record CartDiscount
    {
        //Primary key
        [Key]
        public string Id { get; set; } = string.Empty;

        //Fields
        public required int Value { get; set; }
        public required bool IsPercentage { get; set; }

        //Navigation properties
        public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();
    }
}
