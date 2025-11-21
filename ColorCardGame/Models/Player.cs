using System;
using System.ComponentModel.DataAnnotations;

namespace ColorCardGame.Models
{
    public class Player
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string PlayerId { get; set; } // Cookie-based unique identifier

        [MaxLength(100)]
        public string? DisplayName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActive { get; set; } = DateTime.UtcNow;

        // One-to-one relationship with stats
        public virtual PlayerStats Stats { get; set; } = new PlayerStats();
    }
}