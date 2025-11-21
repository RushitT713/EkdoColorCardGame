using ColorCardGame.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace ColorCardGame.Data
{
    public class GameDbContext : DbContext
    {
        public GameDbContext(DbContextOptions<GameDbContext> options)
            : base(options)
        {
        }

        public DbSet<Player> Players { get; set; }
        public DbSet<PlayerStats> PlayerStats { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Player
            modelBuilder.Entity<Player>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.PlayerId).IsUnique();

                entity.Property(e => e.PlayerId)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.DisplayName)
                    .HasMaxLength(100);

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.LastActive)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // One-to-One relationship with PlayerStats
                entity.HasOne(p => p.Stats)
                    .WithOne(s => s.Player)
                    .HasForeignKey<PlayerStats>(s => s.PlayerId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure PlayerStats
            modelBuilder.Entity<PlayerStats>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.TotalGames)
                    .HasDefaultValue(0);

                entity.Property(e => e.Wins)
                    .HasDefaultValue(0);

                entity.Property(e => e.Losses)
                    .HasDefaultValue(0);
            });
        }
    }
}