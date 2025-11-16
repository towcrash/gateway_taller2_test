using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace censudex_api.src.Models
{
    public class LoginRequest
    {
        public string? Email { get; set; }
        public string? Username { get; set; }
        public string Password { get; set; }
    }
}