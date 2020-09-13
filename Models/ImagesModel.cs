using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace blog.Models
{
    public class ImagesModel
    {
        public ICollection<ImageFile> Images { get; } = new List<ImageFile>();

        [Display(Name = "Files")]
        public ICollection<IFormFile> FormFiles { get; set; }
    }
}
