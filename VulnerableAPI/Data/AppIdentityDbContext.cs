using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VulnerableAPI.Models;

namespace VulnerableAPI.Data
{
    public class AppIdentityDbContext:IdentityDbContext<AppUser,AppRole,string>
    {

        public AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> opts):base(opts)
        {
            
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
          
            base.OnModelCreating(builder);
        }
    }
}
