using BankingManagmentApp.Models;
using BankingManagmentApp.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BankingManagmentApp.Controllers
{
    public class ChatController : Controller
    {
        private readonly ChatService _chatService;
        private readonly UserManager<Customers> _userManager;

        public ChatController(ChatService chatService, UserManager<Customers> userManager)
        {
            _chatService = chatService;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] ChatMessageDto message)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var response = await _chatService.GetResponseAsync(message.Input, user);
            return Json(new { response });
        }
    }

    public class ChatMessageDto
    {
        public string Input { get; set; } = string.Empty;
    }
}
