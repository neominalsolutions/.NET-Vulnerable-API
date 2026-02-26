using Microsoft.AspNetCore.Authorization;

namespace VulnerableAPI.Auth
{
    // Döküman nesnesi ile ilgili herhangi özel bir gereksinim varsa kullanılır.
    // Örneğin, belirli bir kullanıcı rolüne sahip olma gereksinimi gibi.

    // saat 12 ile 16 arasında sadece bu dökümanlara erişim izni verilsin gibi bir gereksinim de olabilir.
    // ABAC (Attribute Based Access Control) için kullanılabilir. user,read,write gibi attribute'ler eklenebilir.
    // document has access level gibi attribute'ler eklenebilir. public,private gibi.


    // Normal User Yetkisine sahip olan kullanıcı sadece kendi dosyalarını görürken
    // Manager yetkisine sahip olan kullanıcı ise o departmandaki tüm dökğmanları görsün. 
    public class DocumentRequirement: IAuthorizationRequirement
    {
        public string? RequiredRole { get; set; }  // manager rolüne bakıcaz

        public DocumentRequirement(){}

        public DocumentRequirement(string requiredRole)
        {
            RequiredRole= requiredRole;
        }
    }
}
