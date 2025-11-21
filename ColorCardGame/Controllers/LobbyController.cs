using ColorCardGame.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ColorCardGame.Controllers
{
    public class LobbyController : Controller
    {
        private readonly IStatsService _statsService;

        public LobbyController(IStatsService statsService)
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
            if (!string.IsNullOrEmpty(playerId))
            {
                var player = await _statsService.GetOrCreatePlayerAsync(playerId);
                ViewBag.PlayerName = player.DisplayName;
            }

            return View();
        }
    }
}