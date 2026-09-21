using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    [Tags("Content")]
    public class ContentController : ControllerBase
    {
        private readonly IContentService _contentService;

        public ContentController(IContentService contentService) => _contentService = contentService;

        /// <summary>All localized static content for a language (en, ur).</summary>
        [HttpGet("{langCode}")]
        public async Task<IActionResult> GetContent(string langCode)
        {
            var items = await _contentService.GetContentAsync(langCode);
            return Ok(new { success = true, langCode, data = items });
        }

        /// <summary>Single content item by key and language.</summary>
        [HttpGet("{langCode}/{contentKey}")]
        public async Task<IActionResult> GetItem(string langCode, string contentKey)
        {
            var item = await _contentService.GetItemAsync(contentKey, langCode);
            return item == null
                ? NotFound(new { success = false, message = "Content not found." })
                : Ok(new { success = true, data = item });
        }
    }
}
