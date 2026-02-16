using BackendApp.Data;
using BackendApp.Models;
using BackendApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConversationService _conversationService;
        private const string UserCookieName = "UserId";

        public UserController(AppDbContext db, IConversationService conversationService)
        {
            _db = db;
            _conversationService = conversationService;
        }

        /// <summary>
        /// Returns the current user ID from the cookie, or creates a new user if none exists.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetOrCreateUser()
        {
            var userId = await ResolveUserIdAsync();
            return Ok(new { userId = userId });
        }

        /// <summary>
        /// Clears the UserId cookie.
        /// </summary>
        [HttpDelete("cookie")]
        public IActionResult ClearCookie()
        {
            Response.Cookies.Delete(UserCookieName, new CookieOptions
            {
                Path = "/",
                SameSite = SameSiteMode.None,
                Secure = true
            });

            return Ok(new { message = "Cookie cleared." });
        }

        /// <summary>
        /// Clears all chat history for the current user.
        /// </summary>
        [HttpDelete("history")]
        public async Task<IActionResult> ClearHistory()
        {
            var cookieValue = Request.Cookies[UserCookieName];
            if (string.IsNullOrEmpty(cookieValue) || !Guid.TryParse(cookieValue, out var userId))
            {
                return BadRequest(new { error = "No valid user cookie found." });
            }

            await _conversationService.ClearHistoryAsync(userId);
            return Ok(new { message = "Chat history cleared." });
        }

        /// <summary>
        /// Resolves the user ID from the cookie or creates a new user.
        /// Sets the cookie on the response if newly created.
        /// </summary>
        internal async Task<Guid> ResolveUserIdAsync()
        {
            var cookieValue = Request.Cookies[UserCookieName];

            if (!string.IsNullOrEmpty(cookieValue) && Guid.TryParse(cookieValue, out var existingId))
            {
                // Verify the user exists in the DB
                var exists = await _db.Users.AnyAsync(u => u.Id == existingId);
                if (exists)
                    return existingId;
            }

            // Create a new user
            var newUser = new User
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(newUser);
            await _db.SaveChangesAsync();

            // Set the cookie
            Response.Cookies.Append(UserCookieName, newUser.Id.ToString(), new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                Path = "/"
            });

            return newUser.Id;
        }
    }
}
