using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VulnerableAPI.Auth;
using VulnerableAPI.Data;
using VulnerableAPI.Models;

namespace VulnerableAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DocumentsController : ControllerBase
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly VulnerableDbContext _context;

        public DocumentsController(IAuthorizationService authorizationService, VulnerableDbContext vulnerableDbContext)
        {
            _authorizationService = authorizationService;
            _context = vulnerableDbContext;
        }


        [HttpGet("{id}")]
        public async Task<IActionResult> GetDocumentAsync(string id)
        {
            Document doc = await _context.Documents.FindAsync(id);

            if(doc == null)
            {
                return NotFound();
            }

            var result = await _authorizationService.AuthorizeAsync(User, doc, new DocumentRequirement());

            if (result.Succeeded)
            {
                return Ok(doc);
            }
            else
            {
                return Forbid();
            }
        }

    }

}
