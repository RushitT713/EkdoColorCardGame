using ColorCardGame.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ColorCardGame.Services
{
    public interface IStatsService
    {
        Task<Player> GetOrCreatePlayerAsync(string playerId, string? displayName = null);
        Task<PlayerStats> GetStatsAsync(string playerId);
        Task RecordGameResultAsync(string playerId, bool isWin);
        Task<List<PlayerStats>> GetLeaderboardAsync(int count = 10);
    }
}