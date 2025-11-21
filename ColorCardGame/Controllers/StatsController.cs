using ColorCardGame.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ColorCardGame.Controllers
{
    public class StatsController : Controller
    {
        private readonly IStatsService _statsService;

        public StatsController(IStatsService statsService)
        {
            _statsService = statsService;
        }

        private string GetPlayerId()
        {
            return HttpContext.Items["PlayerId"]?.ToString() ?? string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var playerId = GetPlayerId();

            if (string.IsNullOrEmpty(playerId))
            {
                return RedirectToAction("Index", "Lobby");
            }

            var myStats = await _statsService.GetStatsAsync(playerId);
            var leaderboard = await _statsService.GetLeaderboardAsync(10);

            ViewBag.MyStats = myStats;
            ViewBag.Leaderboard = leaderboard;
            ViewBag.PlayerId = playerId;

            return View();
        }
    }
}