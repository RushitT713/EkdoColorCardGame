using Microsoft.AspNetCore.Mvc;

namespace ColorCardGame.Controllers
{
    public class ColorCardController : Controller
    {
        [HttpGet]
        public IActionResult Index(string lobbyCode, string playerName)
        {
            ViewBag.LobbyCode = lobbyCode;
            ViewBag.PlayerName = playerName;
            return View();
        }
    }
}