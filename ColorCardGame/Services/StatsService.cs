using ColorCardGame.Data;
using ColorCardGame.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ColorCardGame.Services
{
    public class StatsService : IStatsService
    {
        private readonly GameDbContext _context;
        private readonly ILogger<StatsService> _logger;

        public StatsService(GameDbContext context, ILogger<StatsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Player> GetOrCreatePlayerAsync(string playerId, string? displayName = null)
        {
            var player = await _context.Players
                .Include(p => p.Stats)
                .FirstOrDefaultAsync(p => p.PlayerId == playerId);

            if (player == null)
            {
                player = new Player
                {
                    PlayerId = playerId,
                    DisplayName = displayName ?? $"Player_{playerId.Substring(0, 6)}",
                    CreatedAt = DateTime.UtcNow,
                    LastActive = DateTime.UtcNow,
                    Stats = new PlayerStats()
                };

                _context.Players.Add(player);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Created new player: {playerId} ({player.DisplayName})");
            }
            else
            {
                player.LastActive = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(displayName) && player.DisplayName != displayName)
                {
                    player.DisplayName = displayName;
                }

                await _context.SaveChangesAsync();
            }

            return player;
        }

        public async Task<PlayerStats> GetStatsAsync(string playerId)
        {
            var player = await _context.Players
                .Include(p => p.Stats)
                .FirstOrDefaultAsync(p => p.PlayerId == playerId);

            return player?.Stats ?? new PlayerStats();
        }

        public async Task RecordGameResultAsync(string playerId, bool isWin)
        {
            var player = await _context.Players
                .Include(p => p.Stats)
                .FirstOrDefaultAsync(p => p.PlayerId == playerId);

            if (player == null)
            {
                _logger.LogWarning($"Player {playerId} not found for recording game result");
                return;
            }

            player.Stats.TotalGames++;

            if (isWin)
            {
                player.Stats.Wins++;
            }
            else
            {
                player.Stats.Losses++;
            }

            player.LastActive = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                $"Recorded {(isWin ? "WIN" : "LOSS")} for {player.DisplayName}. " +
                $"Record: {player.Stats.Wins}W-{player.Stats.Losses}L ({player.Stats.WinRate:F1}%)"
            );
        }

        public async Task<List<PlayerStats>> GetLeaderboardAsync(int count = 10)
        {
            return await _context.PlayerStats
                .Include(s => s.Player)
                .Where(s => s.TotalGames > 0)
                .OrderByDescending(s => s.Wins)
                .ThenByDescending(s => s.WinRate)
                .ThenBy(s => s.Losses)
                .Take(count)
                .ToListAsync();
        }
    }
}