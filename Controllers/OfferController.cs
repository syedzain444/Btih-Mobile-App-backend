using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    /// <summary>Public offers &amp; packages catalog for the mobile app.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    [Tags("Offer")]
    public class OfferController : ControllerBase
    {
        private readonly IOfferService _offerService;
        private readonly IMobilePortalSchemaService _schemaService;

        public OfferController(
            IOfferService offerService,
            IMobilePortalSchemaService schemaService)
        {
            _offerService = offerService;
            _schemaService = schemaService;
        }

        /// <summary>Active offers/packages currently in window (sorted).</summary>
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            try
            {
                await _schemaService.EnsureOfferSchemaAsync();
                var items = await _offerService.GetActiveAsync();
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

        /// <summary>Single offer detail (active or inactive — app should prefer /active list).</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                await _schemaService.EnsureOfferSchemaAsync();
                var item = await _offerService.GetByIdAsync(id);
                if (item == null)
                {
                    return NotFound(new { success = false, message = "Offer not found" });
                }

                return Ok(new { success = true, data = MapPublic(item) });
            }
            catch (Exception ex) when (DatabaseExceptionHelper.TryGetFriendlyMessage(ex, out var dbMessage, out var statusCode))
            {
                return StatusCode(statusCode, new { success = false, message = dbMessage });
            }
        }

        private static object MapPublic(MobileOfferRecord o) => new
        {
            offerId = o.OfferId,
            title = o.Title,
            subtitle = o.Subtitle,
            description = o.Description,
            category = o.Category,
            imageUrl = o.ImageUrl,
            originalPrice = o.OriginalPrice,
            offerPrice = o.OfferPrice,
            currency = o.Currency,
            highlights = o.HighlightList,
            ctaLabel = o.CtaLabel,
            ctaPhone = o.CtaPhone,
            sortOrder = o.SortOrder,
            startAt = o.StartAt,
            endAt = o.EndAt,
        };
    }
}
