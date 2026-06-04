namespace Eliteracingleague.API.DTOs.Admin
{
    public class AdminUserResponse
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool EmailVerified { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}