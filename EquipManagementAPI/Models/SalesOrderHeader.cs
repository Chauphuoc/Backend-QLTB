using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Sales Order Header")]
    public class SalesOrderHeader
    {
        [Key]
        [Column("No_")]
        public string No { get; set; }

        [Column("PO Customer Name")]
        public string POName { get; set; }
        [Column("Status")] 
        public int Status { get; set; }
        [Column("Sell-to Customer No_")]
        public string SellToCustomerNo { get; set; } = string.Empty;
        [Column("Order Date")]
        public DateTime? OrderDate { get; set; }
        [Column("Type")]
        public int Type { get; set; }
        [Column("Requested Delivery Date")]
        public DateTime? RequestedDeliveryDate { get; set; }
        [Column("Login ID")]
        public string LoginId { get; set; }
        
    }
}
