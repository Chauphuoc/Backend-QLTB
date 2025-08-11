using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Department")]
    public class Department
    {
        [Column("RowID")]
        public int Id { get; set; }
        public string? Address { get; set; }
        [Column("Address 2")]
        public string? Address2 { get; set; }
        public string? City { get; set; }
        public string Code { get; set; }
        public string? Contact { get; set; }
        [Column("Contact 2")]
        public string? Contact2 { get; set; }
        [Column("County Code")]
        public string? CountyCode { get; set; }
        public string? Description { get; set; }

        public string? Email { get; set; }
        [Column("Fax No_")]
        public string? Fax { get; set; }
        [Column("Home Page")]
        public string? HomePage { get; set; }
        [Column("Manager No_")]
        public string? ManagerNo { get;set; }
        public string? Name { get; set; }
        [Column("Phone No_")]
        public string? PhoneNo { get; set; }
        [Column("Post Code")]
        public string? PostCode { get; set;}
        [Column("Responsibility Center")]
        public string? Responsibility { get; set; }
        [Column("Source Code")]
        public string? SourceCode { get; set; }
        [Column("Location Code")]
        public string? LocationCode { get; set; }
        [Column("WorkCenter No_")]
        public string? WorkCenter { get; set;}
    }
}
