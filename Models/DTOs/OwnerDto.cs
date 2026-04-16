namespace SmartKostanay.Models.DTOs
{
    public class OwnerDto
    {
        public string Lastname { get; set; } = string.Empty;
        public string Firstname { get; set; } = string.Empty;
        public string? Patronymic { get; set; } 

        public string IdentityNumber { get; set; } = string.Empty; 
        public string? PhoneNumber { get; set; } 
    }
}