using System.ComponentModel.DataAnnotations.Schema;

namespace EquipManagementAPI.Models;

[Table("Equipment Group")]
public class EquipmentGroup
{
    [Column("RowID")]
    public int Id { get; set; }

    public string? Code { get; set; }

    public string Name { get; set; }

    [Column("Asset Type Code")]
    public string AssetTypeCode { get; set; }

    public string Description { get; set; }

    [Column("Equip Sub Code")]
    public string EquipsubCode { get; set; }
}
