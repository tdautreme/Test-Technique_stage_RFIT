using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace RFIT
{
    public class Material
    {
        public int Id { get; set; }
        [Required]
        [MaxLength(20, ErrorMessage ="Material name length cannot be greater than 20")]
        public string Name { get; set; }
        [Required]
        [MaxLength(20, ErrorMessage = "Material serial number length cannot be greater than 20")]
        [RegularExpression("([0-9]+)", ErrorMessage = "Material serial number must be numeric string")]
        public string SerialNumber { get; set; }
        [Required]
        public long InspectionDate { get; set; }
        [MaxLength(70, ErrorMessage = "Can't add more image to the server, file path length too long")]
        public string ImagePath { get; set; }
    }

    public class MaterialDbContext : DbContext
    {
        public MaterialDbContext(DbContextOptions<MaterialDbContext> options) : base(options)
        { }
        public DbSet<Material> Materials { get; set; }
    }
}