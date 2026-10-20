using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace EquipManagementAPI.Models
{
    [Table("Sales Order Line")]
    public class SalesOrderLine
    {
        [Key]
        [Column("RowID")]
        public int RowID { get; set; }

        [Column("Document Type")]
        public int DocumentType { get; set; }

        [Column("Document No_")]
        [MaxLength(100)]
        public string DocumentNo { get; set; } = string.Empty;

        [Column("Line No_")]
        public int LineNo { get; set; }

        [Column("Sell-to Customer No_")]
        public string SellToCustomerNo { get; set; } = string.Empty;

        [Column("Type")]
        public int Type { get; set; }

        [Column("No_")]
        [MaxLength(100)]
        public string No { get; set; } = string.Empty;

        [Column("Location Code")]
        [MaxLength(100)]
        public string LocationCode { get; set; } = string.Empty;

        [Column("Posting Group")]
        [MaxLength(100)]
        public string PostingGroup { get; set; } = string.Empty;

        [Column("Shipment Date")]
        public DateTime? ShipmentDate { get; set; }


        [Column("Description 2")]
        [MaxLength(100)]
        public string Description2 { get; set; } = string.Empty;

        [Column("Unit of Measure")]
        [MaxLength(100)]
        public string? UnitOfMeasure { get; set; }
        [Column("Color No_")]
        public string ColorNo { get; set; }
        [Column("Quantity")]
        public int Quantity { get; set; }
    }
}
