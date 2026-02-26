namespace VulnerableAPI.Models
{
    public class Document
    {
        public string Id { get; init; }
        public string UserId { get; set; } // kime ait
        public string Title { get; set; }
        public string Content { get; set; }

        // DepartmentId  Hangi departman için tanımlanmış.

        public Document() { 
          Id = Guid.NewGuid().ToString();
        }
    }
}
