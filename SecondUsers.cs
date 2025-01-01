using System.ComponentModel.DataAnnotations;

namespace Islemler.Models
{
    public class SecondUsers
    {
        /// <summary>
        /// Kullanıcı ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Kullanıcı adı
        /// </summary>
        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Email adresi
        /// </summary>
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Yaş
        /// </summary>
        [Range(0, 150, ErrorMessage = "Age must be between 0 and 150")]
        public int Age { get; set; }
    }
} 