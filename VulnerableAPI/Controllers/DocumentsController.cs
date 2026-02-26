using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
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
        private readonly IDataProtector _dataProtector;
        private readonly VulnerableDbContext _context;

        public DocumentsController(IAuthorizationService authorizationService, VulnerableDbContext vulnerableDbContext,IDataProtectionProvider dataProtectionProvider)
        {
            _authorizationService = authorizationService;
            _context = vulnerableDbContext;
            _dataProtector = dataProtectionProvider.CreateProtector("DocumentIdProtector");
        }

        // bir endpoint ile tüm dökğmanları dto üzerindne dışarı çıkaralım. ve Data Protection API ile Idleri koruma altına alalım.


        [HttpGet]
        public async Task<IActionResult> GetDocumentsAsync()
        {
            var documents = _context.Documents.ToList();
            var result = new List<DocumentDto>();

          

            foreach (var doc in documents)
            {
                var authResult = await _authorizationService.AuthorizeAsync(User, doc, new DocumentRequirement());
                if (authResult.Succeeded)
                {
                    result.Add(new DocumentDto
                    {
                        Id = _dataProtector.Protect(doc.Id),
                        Title = doc.Title,
                        Content = doc.Content
                    });
                }
            }
            return Ok(result);
        }


        [HttpGet("{id}")]
        public async Task<IActionResult> GetDocumentAsync(string id) // id protector Id
        {

            string docId = _dataProtector.Unprotect(id);
            Document doc = await _context.Documents.FindAsync(docId);

            if(doc == null)
            {
                return NotFound();
            }

            var result = await _authorizationService.AuthorizeAsync(User, doc, new DocumentRequirement());

            if (result.Succeeded)
            {
                return Ok(new DocumentDto
                {
                    Id = id,
                    Title = doc.Title,
                    Content = doc.Content
                });
            }
            else
            {
                return Forbid();
            }
        }

    }

}
