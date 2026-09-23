using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models
{
    [Table("Currency Exchange Rate")]
    public class CurrencyExchangeRates
    {
        [Key]
        public int RowID { get; set; }

        [Column("Currency Code")]
        public string CurrencyCode { get; set; }

        [Column("Starting Date")]
        public DateTime StartingDate { get; set; }

        [Column("Exchange Rate Amount")]
        public decimal ExchangeRateAmount { get; set; }

        [Column("Adjustment Exch_ Rate Amount")]
        public decimal AdjustmentExchRateAmount { get; set; }

        [Column("Relational Currency Code")]
        public string RelationalCurrencyCode { get; set; }

        [Column("Relational Exch_ Rate Amount")]
        public decimal RelationalExchRateAmount { get; set; }

        [Column("Fix Exchange Rate Amount")]
        public int FixExchangeRateAmount { get; set; }

        [Column("Relational Adjmt Exch Rate Amt")]
        public decimal RelationalAdjmtExchRateAmt { get; set; }

        [Column("Relational Exch_ Rate Amount Local")]
        public decimal? RelationalExchRateAmountLocal { get; set; }

        [Column("Local Exchange")]
        public decimal LocalExchange { get; set; }
    }
}
