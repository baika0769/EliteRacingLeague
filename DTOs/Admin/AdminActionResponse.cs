namespace Eliteracingleague.API.DTOs.Admin
{
    public class AdminActionResponse
    {
        public string Message { get; set; } = string.Empty;
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
    }
}