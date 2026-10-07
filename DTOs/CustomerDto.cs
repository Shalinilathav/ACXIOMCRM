using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.DTOs;

public class CustomerDto
{
    public int CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class CreateCustomerDto
{
    [Required, MaxLength(100)] public string CustomerName { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(150)] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(15)] public string Phone { get; set; } = string.Empty;
    [MaxLength(150)] public string? CompanyName { get; set; }
    [MaxLength(250)] public string? Address { get; set; }
    [MaxLength(100)] public string? City { get; set; }
    [MaxLength(100)] public string? State { get; set; }
    public string? Notes { get; set; }
    public string? AssignedTo { get; set; }
}

public class UpdateCustomerDto
{
    [Required, MaxLength(100)] public string CustomerName { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(150)] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(15)] public string Phone { get; set; } = string.Empty;
    [MaxLength(150)] public string? CompanyName { get; set; }
    [MaxLength(250)] public string? Address { get; set; }
    [MaxLength(100)] public string? City { get; set; }
    [MaxLength(100)] public string? State { get; set; }
    public string Status { get; set; } = "Active";
    public string? Notes { get; set; }
    public string? AssignedTo { get; set; }
}
