using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    /// <summary>Public launch promotions for the mobile app carousel.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    [Tags("Promotion")]
    public class PromotionController : ControllerBase
    {
        private readonly IPromotionService _promotionService;

        public PromotionController(IPromotionService promotionService)
        {
            _promotionService = promotionService;
        }

        /// <summary>Active promotions for splash → welcome carousel (sorted).</summary>
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            try
            {
                var items = await _promotionService.GetActiveAsync();
                return Ok(new
                {
                    success = true,
                    count = items.Count,
                    data = items.Select(MapPublic),
                });
            }
            catch (Exception ex) when (DatabaseExceptionHelper.TryGetFriendlyMessage(ex, out var dbMessage, out var statusCode))
            {
                return StatusCode(statusCode, new { success = false, message = dbMessage });
            }
        }

        private static object MapPublic(MobilePromotionRecord p) => new
        {
            promotionId = p.PromotionId,
            title = p.Title,
            imageUrl = p.ImageUrl,
            sortOrder = p.SortOrder,
            durationSeconds = p.DurationSeconds <= 0 ? 5 : p.DurationSeconds,
        };
    }
}
