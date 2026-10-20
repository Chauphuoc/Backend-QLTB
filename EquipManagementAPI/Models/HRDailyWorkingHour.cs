using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("HR Daily Working Hour")]
    public class HRDailyWorkingHour
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RowID { get; set; }

        [Required]
        public DateTime DateIn { get; set; }

        [Required]
        [Column("No_")]
        public string No_ { get; set; }

        public string? Name { get; set; }

        [Column("Work Center Code")]
        public string? WorkCenterCode { get; set; }

        [Column("Work Shop Code")]
        public string? WorkShopCode { get; set; }

        [Required]
        [Column("Work Shift Code")]
        public string WorkShiftCode { get; set; }

        [Column("Work Shift Place")]
        public string? WorkShiftPlace { get; set; }

        [Column("Position Code")]
        public string? PositionCode { get; set; }

        [Column("Contract Code")]
        public string? ContractCode { get; set; }

        public DateTime? FirstIn { get; set; }

        public DateTime? SecondOut { get; set; }

        public decimal? DailyHour { get; set; }

        public decimal? WorkingHour { get; set; }

        public decimal? OverTime { get; set; }

        [Column("CauseOfOverTime")]
        public string? CauseOfOverTime { get; set; }

        [Column("OverDescription")]
        public string? OverDescription { get; set; }

        public decimal? AbsenceTime { get; set; }

        [Column("CauseOfAbsenceTime")]
        public string? CauseOfAbsenceTime { get; set; }

        [Column("AbsenceDescription")]
        public string? AbsenceDescription { get; set; }

        public string? Note { get; set; }

        public int? Status { get; set; }

        [Column("User Approve")]
        public string? UserApprove { get; set; }

        [Column("User Create DateTime")]
        public DateTime? UserCreateDateTime { get; set; }

        [Column("Work Day")]
        public decimal? WorkDay { get; set; }

        [Column("Rest Time")]
        public decimal? RestTime { get; set; }

        [Required]
        public string WorkShift { get; set; }

        [Column("Work Night")]
        public decimal? WorkNight { get; set; }

        [Column("Root SecondOut")]
        public DateTime? RootSecondOut { get; set; }

        [Column("Root FirstIn")]
        public DateTime? RootFirstIn { get; set; }

        [Column("Root Work Day")]
        public decimal? RootWorkDay { get; set; }

        [Column("Root Rest Time")]
        public decimal? RootRestTime { get; set; }

        [Column("Root WorkingHour")]
        public decimal? RootWorkingHour { get; set; }

        [Column("Root DailyHour")]
        public decimal? RootDailyHour { get; set; }

        [Column("Root OverTime")]
        public decimal? RootOverTime { get; set; }

        [Column("Root CauseOfOverTime")]
        public string? RootCauseOfOverTime { get; set; }

        [Column("Root OverDescription")]
        public string? RootOverDescription { get; set; }

        [Column("Root AbsenceTime")]
        public decimal? RootAbsenceTime { get; set; }

        [Column("Root CauseOfAbsenceTime")]
        public string? RootCauseOfAbsenceTime { get; set; }

        [Column("Root AbsenceDescription")]
        public string? RootAbsenceDescription { get; set; }

        [Column("Root Work Night")]
        public decimal? RootWorkNight { get; set; }


        [Column("Work Noon")]
        public decimal? WorkNoon { get; set; }

        public int? Standard { get; set; }
    }
}
