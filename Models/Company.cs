using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Isdocovac.Models.Enums;

namespace Isdocovac.Models;

[Table("companies")]
public class Company
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid OwnerUserId { get; set; }

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public bool IsLegalEntity { get; set; } = true;

    [MaxLength(255)]
    public string? CompanyName { get; set; }

    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    [MaxLength(50)]
    public string? Titul { get; set; }

    [MaxLength(20)]
    public string? Dic { get; set; }

    [MaxLength(20)]
    public string? Ico { get; set; }

    [MaxLength(255)]
    public string? Street { get; set; }

    [MaxLength(20)]
    public string? HouseNumber { get; set; }

    [MaxLength(20)]
    public string? OrientNumber { get; set; }

    [MaxLength(255)]
    public string? City { get; set; }

    [MaxLength(20)]
    public string? Zip { get; set; }

    [MaxLength(2)]
    public string CountryCode { get; set; } = "CZ";

    [MaxLength(255)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    public int? TaxOfficeCode { get; set; }

    public int? TaxOfficeBranchCode { get; set; }

    [MaxLength(10)]
    public string? OkecCode { get; set; }

    [MaxLength(20)]
    public string? DataBoxId { get; set; }

    [Required]
    public VatPeriod VatPeriod { get; set; } = VatPeriod.Monthly;

    [Required]
    public bool IsActive { get; set; } = true;

    [Required]
    public DateTime CreatedAt { get; set; }

    [Required]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey(nameof(OwnerUserId))]
    public User Owner { get; set; } = null!;
}
