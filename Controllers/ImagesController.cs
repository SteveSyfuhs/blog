using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using blog.Models;
using Imageflow.Fluent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace blog.Controllers
{
    [Authorize]
    public class ImagesController : Controller
    {
        private readonly IBlogService _blog;

        public ImagesController(IBlogService blog)
        {
            this._blog = blog;
        }

        [Route("/edit/images")]
        [HttpGet]
        public async Task<IActionResult> ListImages()
        {
            ViewData["Title"] = "Image Manager";

            var images = await _blog.ListImages();

            return View("images", images);
        }

        [Route("/edit/upload")]
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public async Task<IActionResult> UploadEditorImage(IFormFile file)
        {
            const long maxUploadSize = 10L * 1024L * 1024L * 1024L;

            if (!ModelState.IsValid)
            {
                return View("images");
            }

            string name = null;

            var formFileContent = await FileHelpers.ProcessFormFile<ImagesModel>(
                    file,
                    ModelState, new[] { ".jpg", ".jpeg", ".png", ".gif", ".jfif" },
                    maxUploadSize
                );

            if (formFileContent.Length > 0)
            {
                name = await UploadImage(formFileContent, file.FileName, $"{DateTimeOffset.UtcNow.Year}/{DateTimeOffset.UtcNow.Month}");
            }

            return Json(new { location = name });
        }

        [Route("/edit/images")]
        [HttpDelete]
        public async Task<IActionResult> DeleteImage([FromQuery] string url)
        {
            await this._blog.DeleteFile(url);

            return StatusCode((int)HttpStatusCode.NoContent);
        }

        [Route("/edit/images")]
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public async Task<IActionResult> UploadImage(ImagesModel model)
        {
            const long maxUploadSize = 10L * 1024L * 1024L * 1024L;

            if (!ModelState.IsValid)
            {
                return View("images");
            }

            string name = null;

            foreach (var file in model.FormFiles)
            {
                var formFileContent = await FileHelpers.ProcessFormFile<ImagesModel>(
                    file,
                    ModelState, new[] { ".jpg", ".jpeg", ".png", ".gif", ".jfif" },
                    maxUploadSize
                );

                if (formFileContent.Length > 0)
                {
                    name = await UploadImage(formFileContent, file.FileName, model.UploadFolder);

                    name = Path.GetFileNameWithoutExtension(name);
                }
            }

            return Redirect($"/edit/images");
        }

        private async Task<string> UploadImage(byte[] formFileContent, string name, string uploadFolder)
        {
            name = name.Replace(" ", "_");

            if (!string.IsNullOrWhiteSpace(uploadFolder))
            {
                name = WebUtility.UrlEncode($"{uploadFolder}/{name}");
            }

            await DuplicateSmallerImageIfLargeAndSave(formFileContent, name, ifLargerThan: 1024, constrainX: 500, constrainY: 400);

            return await EncodeToPreferredFormatAndSave(formFileContent, name);
        }

        private async Task<string> EncodeToPreferredFormatAndSave(byte[] formFileContent, string name)
        {
            using (var job = new ImageJob())
            {
                var info = await ImageJob.GetImageInfo(new BytesSource(formFileContent));

                GetEncoder(info, out IEncoderPreset encoder, out string expectedExt);

                var resized = await job.Decode(formFileContent)
                                           .EncodeToBytes(encoder)
                                           .Finish()
                                           .InProcessAndDisposeAsync();

                var result = resized.First.TryGetBytes();

                if (result.HasValue)
                {
                    var fileName = Path.GetFileName(name);
                    var ext = Path.GetExtension(fileName);

                    var newFileName = name.Replace(ext, expectedExt);

                    return await _blog.SaveFile(result.Value.Array, newFileName);
                }
            }

            return null;
        }

        private async Task DuplicateSmallerImageIfLargeAndSave(byte[] formFileContent, string name, int ifLargerThan, int constrainX, int constrainY)
        {
            using (var job = new ImageJob())
            {
                var info = await ImageJob.GetImageInfo(new BytesSource(formFileContent));

                if (info.ImageWidth > ifLargerThan)
                {
                    GetEncoder(info, out IEncoderPreset encoder, out string expectedExt);

                    var resized = await job.Decode(formFileContent)
                                           .ConstrainWithin((uint)constrainX, (uint)constrainY)
                                           .EncodeToBytes(encoder)
                                           .Finish()
                                           .InProcessAndDisposeAsync();

                    var result = resized.First.TryGetBytes();

                    if (result.HasValue)
                    {
                        var fileName = Path.GetFileName(name);
                        var ext = Path.GetExtension(fileName);

                        var newFileName = name.Replace(ext, "-sm" + expectedExt);

                        await _blog.SaveFile(result.Value.Array, newFileName);
                    }
                }
            }
        }

        private static void GetEncoder(Imageflow.Bindings.ImageInfo info, out IEncoderPreset encoder, out string expectedExt)
        {
            if (string.Equals("image/png", info.PreferredMimeType, StringComparison.OrdinalIgnoreCase))
            {
                encoder = new PngQuantEncoder(100, 80);
                expectedExt = ".png";
            }
            else
            {
                encoder = new MozJpegEncoder(80, true);
                expectedExt = ".jpg";
            }
        }
    }
}
