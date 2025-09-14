using System;
using System.ComponentModel.DataAnnotations;

namespace BankingManagmentApp.Models
{
    public class Feedback
    {
        public int Id { get; set; }

        [Required]
        [StringLength(1000)]
        public string Comment { get; set; } = string.Empty;

        // optional: track who/when gave feedback
        public string? UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
