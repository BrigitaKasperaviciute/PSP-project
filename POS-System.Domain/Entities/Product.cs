using POS_System.Domain.Entities.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS_System.Domain.Entities
{
    [Table("Products")]
    public record Product : ILinkable
    {
        //Primary key
        [Key]
        public int Id { get; set; }

        //Navigation properties
        public virtual ICollection<ProductOnTax> ProductOnTaxes { get; set; } = new List<ProductOnTax>();
        public virtual ICollection<ProductOnItemDiscount> ProductOnItemDiscounts { get; set; } = new List<ProductOnItemDiscount>();
        public virtual ICollection<ProductModification> ProductModifications { get; set; } = new List<ProductModification>();

        //Fields
        public int ProductId { get; set; }
        [MaxLength(40)]
        public required string Name { get; set; }
        public required string Description { get; set; }
        public required int Price { get; set; }
        public required string ImageURL { get; set; }
        public required int Stock { get; set; }

        //Versioning
        public DateTime Version { get; set; }
        public bool IsDeleted { get; set; }

    }
}
