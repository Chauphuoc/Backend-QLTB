using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Sales Order Line Detail Factory")]
    public class SalesOrderLineDetailFactory
    {
        [Key]
        public int RowID { get; set; }

        [Column("Document No_")]
        public string? DocumentNo { get; set; }

        [Column("SOL Line No_")]
        public int? SolLineNo { get; set; }

        [Column("Color No_")]
        public string? ColorNo { get; set; }

        [Column("Size No_")]
        public string? SizeNo { get; set; }

        [Column("Work Center Code")]
        public string? WorkCenterCode { get; set; }

        public int? Quantity { get; set; }

        [MaxLength(250)]
        public string? Description { get; set; }

        public int? Status { get; set; }

        [Column("Unit of Measure")]
        public string? UnitOfMeasure { get; set; }

        [Column("Qty_ per Unit of Measure")]
        public decimal? QtyPerUnitOfMeasure { get; set; }
    }
}
