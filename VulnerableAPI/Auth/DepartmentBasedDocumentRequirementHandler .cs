using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using VulnerableAPI.Models;

namespace VulnerableAPI.Auth
{
    // Dökümanı oluşturan dökümana erişim sağlar. Sadece dökümanı oluşturan kullanıcı erişebilir.
    public class DepartmentBasedDocumentRequirementHandler : AuthorizationHandler<DocumentRequirement, Document>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, DocumentRequirement requirement, Document resource)
        {

            // kullanıcı sadece kendi oluşturduğu belgelere erişebilir olsun;
            // Token içerisinde kullanıcı id'si olduğunu varsayıyoruz.
            var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                context.Fail();
                return Task.CompletedTask;
            }

            // Bu kişi tüm dosylara erişebilir. 

            // if(requirement.RequiredRole.Contains("Manager") && resource.DepartmentId == "userDepartmentId"){
            //context.Succeed(requirement);
            // return Task.CompletedTask;
            //}
            /* else  
            // if (userId == resource.UserId) // sadece kendine ait olana erişebilir.
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
            else
            {
                context.Fail();
                return Task.CompletedTask;
            }
            */



            var userId = userIdClaim.Value;

            if (userId == resource.UserId)
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
            else
            {
                context.Fail();
                return Task.CompletedTask;
            }

        }
    }
}
