using System.ComponentModel.DataAnnotations;

namespace Five68.Models.Authentication
{
    public class InviteAccept
    {
        [Required]
        public string Token { get; set; }
        [Required]
        public string Password { get; set; }
    }
}
