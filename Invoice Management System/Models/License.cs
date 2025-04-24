using System;
using System.ComponentModel.DataAnnotations;

namespace Invoice_Management_System
{
    public class License
    {
        public int Id { get; set; }
        public string LicenseKey { get; set; } = string.Empty;
        public DateTime ActivationDate { get; set; }
        public bool IsActivated { get; set; }
    }



}
