using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Mvc;

namespace blog
{
    public class SearchController : Controller
    {
        private readonly IBlogService _blog;
        private readonly BlogSettings _settings;

        public SearchController(IBlogService blog)
        {
            _blog = blog;
            _settings = _blog.Settings;
        }

        [Route("/opensearch.xml")]
        public async Task OpenSearch()
        {
            string host = Request.Scheme + "://" + Request.Host;

            Response.ContentType = "application/xml";

            await using (var xml = XmlWriter.Create(Response.Body, new XmlWriterSettings { Async = true, Indent = true }))
            {
                xml.WriteStartDocument();
                xml.WriteStartElement("OpenSearchDescription", "http://a9.com/-/spec/opensearch/1.1/");
                xml.WriteStartElement("ShortName");
                xml.WriteString(_settings.Name);
                xml.WriteEndElement();
                xml.WriteStartElement("Description");
                xml.WriteString($"Search {_settings.Name} - {_settings.Description}");
                xml.WriteEndElement();
                xml.WriteStartElement("InputEncoding");
                xml.WriteString("UTF-8");
                xml.WriteEndElement();
                xml.WriteStartElement("Image");
                xml.WriteAttributeString("width", "16");
                xml.WriteAttributeString("height", "16");
                xml.WriteAttributeString("type", "image/x-icon");
                xml.WriteString($"{host}/icon16x16.png");
                xml.WriteEndElement();
                xml.WriteStartElement("Url");
                xml.WriteAttributeString("type", "text/html");
                xml.WriteAttributeString("method", "get");
                xml.WriteAttributeString("template", $"{host}/search?q={{searchTerms}}");
                xml.WriteEndElement();

                xml.WriteEndElement();
            }

            await Response.Body.FlushAsync();
        }

        [Route("/search")]
        public async Task<IActionResult> Index([FromQuery] string q, [FromQuery] int page)
        {
            ViewData["Title"] = _settings.Name + " | " + _settings.Description;
            ViewData["Description"] = "Search the site: " + _settings.Name;

            if (string.IsNullOrWhiteSpace(q))
            {
                return View();
            }

            ViewData["Title"] = q + " | " + ViewData["Title"];

            ViewData["Description"] += $" for {q}";

            if (page <= 0)
            {
                page = 0;
            }

            var pageResults = _settings.PostsPerPage;

            var skip = page * pageResults;

            var results = await _blog.Search(q, skip, pageResults);

            results.Page = page;

            if (skip + results.Posts.Count() < results.Results)
            {
                ViewData["prev"] = $"/search?q={q}&page={page + 1}";
            }

            ViewData["next"] = $"/search?q={q}&page={page - 1}";

            return View(results);
        }
    }
}