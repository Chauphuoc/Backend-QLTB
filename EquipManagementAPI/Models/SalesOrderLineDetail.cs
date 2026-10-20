using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Sales Order Line Detail")]
    public class SalesOrderLineDetail
    {
        [Key]
        public int RowID { get; set; }

        [Column("Document No_")]
        [StringLength(50)]
        public string DocumentNo { get; set; } = string.Empty;

        [Column("Line No_")]
        public int LineNo { get; set; }

        [Column("Item No_")]
        [StringLength(50)]
        public string ItemNo { get; set; } = string.Empty; 
        [Column("Item Line No_")]
        public int ItemLineNo { get; set; }

        [Column("Item Art")]
        [StringLength(50)]
        public string ItemArt { get; set; } = string.Empty; 

        [Column("Item SubArt")]
        [StringLength(50)]
        public string ItemSubArt { get; set; } = string.Empty; 

        [Column("Color No_")]
        [StringLength(50)]
        public string ColorNo { get; set; } = string.Empty; 

        [Column("Size No_")]
        [StringLength(50)]
        public string SizeNo { get; set; } = string.Empty; 

        [StringLength(50)]
        public string Separation { get; set; } = string.Empty;

        [Column("Size Group")]
        [StringLength(50)]
        public string SizeGroup { get; set; } = string.Empty;

        public int? Quantity { get; set; }

        [StringLength(250)]
        public string? Description { get; set; }
        

        [Column("Unit of Measure")]
        [StringLength(50)]
        public string? UnitofMeasure { get; set; }
        

        [Column("Detail No_")]
        [StringLength(50)]
        public string? DetailNo { get; set; }
        

        [Column("Production BOM No_")]
        [StringLength(50)]
        public string? ProductionBOMNo { get; set; }
       

        public int? Status { get; set; }
    }
}
