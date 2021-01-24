using blog.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.SyndicationFeed;
using Microsoft.SyndicationFeed.Atom;
using Microsoft.SyndicationFeed.Rss;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using static blog.ArinMiddleware;

namespace blog
{
    public class RobotsController : Controller
    {
        private readonly IBlogService _blog;
        private readonly IOptionsSnapshot<BlogSettings> _settings;

        public RobotsController(IBlogService blog, IOptionsSnapshot<BlogSettings> settings)
        {
            _blog = blog;
            _settings = settings;
        }

        [Route("/old/{path?}")]
        [Route("/new/{path?}")]
        [Route("/wordpress/{path?}")]
        [Route("/demo/{path?}")]
        [Route("/site/{path?}")]
        [Route("/wallet.dat")]
        [Route("/bitcoin/{path?}")]
        [Route("/wp-admin/{path?}")]
        [Route("{file}.php")]
        [Route("/wp-json/wp/v2/{resource?}")]
        [Route("/inc/{file?}")]
        [Route("/administrator/")]
        public IActionResult Sinkhole()
        {
            return new EmptyResult();
        }

        [Route("/post.aspx")]
        [Route("/index.php")]
        [Route("/aggbug.ashx")]
        [Route("/article/{id}.aspx")]
        [Route("/author/{author}/")]
        public IActionResult AggBug()
        {
            return Redirect("~/");
        }

        [Route("/status")]
        public async Task<IActionResult> Status()
        {
            var ip = this.HttpContext.Connection.RemoteIpAddress.ToString();

            Cache.TryGetValue(ip, out AddressCacheItem value);

            return Json(new
            {
                Cache.Count,
                Uptime = (DateTimeOffset.UtcNow - Start).ToString(),
                PostCount = await _blog.GetPostCount(includePages: true),
                Caller = ip,
                this.HttpContext.Connection.Id,
                Network = value.Value
            });
        }

        [Route("/robots.txt")]
        [OutputCache(Profile = "default")]
        public string RobotsTxt()
        {
            string host = Request.Scheme + "://" + Request.Host;
            var sb = new StringBuilder();

            sb.AppendLine("User-agent: dotbot");
            sb.AppendLine("Disallow: /");
            sb.AppendLine("User-agent: rogerbot");
            sb.AppendLine("Disallow: /");
            sb.AppendLine("User-agent: *");
            sb.AppendLine("Disallow:");

            sb.AppendLine($"sitemap: {host}/sitemap.xml");

            return sb.ToString();
        }

        [Route("/sitemap.txt")]
        public async Task SiteMapText()
        {
            string host = Request.Scheme + "://" + Request.Host;

            Response.ContentType = "text/plain";

            using (var body = new StreamWriter(Response.Body))
            {
                var posts = await _blog.GetPosts(int.MaxValue);

                foreach (Post post in posts)
                {
                    await body.WriteLineAsync(host + post.GetLink());
                }
            }
        }

        [Route("/author-sitemap.xml")]
        [Route("/sitemap.axd")]
        [Route("/image-sitemap-{index}.xml")]
        [Route("/sitemap-{index}.xml")]
        [Route("/sitemap.xml")]
        public async Task SitemapXml(string index = "1")
        {
            string host = Request.Scheme + "://" + Request.Host;

            Response.ContentType = "application/xml";

            await using (var xml = XmlWriter.Create(Response.Body, new XmlWriterSettings { Async = true, Indent = true }))
            {
                xml.WriteStartDocument();
                xml.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

                var posts = await _blog.GetPosts(int.MaxValue);

                foreach (Post post in posts)
                {
                    var lastMod = new[] { post.PubDate, post.LastModified };

                    xml.WriteStartElement("url");
                    xml.WriteElementString("loc", host + post.GetLink());
                    xml.WriteElementString("lastmod", lastMod.Max().ToString("yyyy-MM-ddThh:mmzzz"));
                    xml.WriteEndElement();
                }

                xml.WriteEndElement();
            }

            await Response.Body.FlushAsync();
        }

        [Route("/rsd.xml")]
        public void RsdXml()
        {
            string host = Request.Scheme + "://" + Request.Host;

            Response.ContentType = "application/xml";
            Response.Headers["cache-control"] = "no-cache, no-store, must-revalidate";

            using (var xml = XmlWriter.Create(Response.Body, new XmlWriterSettings { Indent = true }))
            {
                xml.WriteStartDocument();
                xml.WriteStartElement("rsd");
                xml.WriteAttributeString("version", "1.0");

                xml.WriteStartElement("service");

                xml.WriteElementString("enginename", "blog");
                xml.WriteElementString("enginelink", "https://syfuhs.net/");
                xml.WriteElementString("homepagelink", host);

                xml.WriteStartElement("apis");
                xml.WriteStartElement("api");
                xml.WriteAttributeString("name", "MetaWeblog");
                xml.WriteAttributeString("preferred", "true");
                xml.WriteAttributeString("apilink", host + "/metaweblog");
                xml.WriteAttributeString("blogid", "1");

                xml.WriteEndElement(); // api
                xml.WriteEndElement(); // apis
                xml.WriteEndElement(); // service
                xml.WriteEndElement(); // rsd
            }
        }

        [Route("/comments/feed/{type?}")]
        public async Task CommentsFeed(string type = "rss")
        {
            var posts = await _blog.GetPosts(int.MaxValue);

            var comments = posts.SelectMany(p => p.Comments);

            await SerializeFeed(
                type,
                comments,
                () => comments.OrderByDescending(c => c.PubDate).Select(c => c.PubDate).FirstOrDefault(),
                SerializeComment
            );
        }

        [Route("/atom.aspx")]
        public async Task Atom()
        {
            await Rss("atom");
        }

        [Route("/SyndicationService.asmx/GetRss")]
        [Route("/rss.aspx")]
        [Route("/syndication.axd")]
        [Route("/feed/{type?}")]
        public async Task Rss(string type = "rss")
        {
            var posts = await _blog.GetPosts(25);

            await SerializeFeed(
                type,
                posts,
                lastUpdated: () => posts.Max(p => p.PubDate),
                serializer: SerializePost
            );
        }

        private async Task SerializeFeed<T>(string type, IEnumerable<T> items, Func<DateTime> lastUpdated, Func<string, T, AtomEntry> serializer)
        {
            Response.ContentType = "application/xml";
            string host = Request.Scheme + "://" + Request.Host;

            await using (XmlWriter xmlWriter = XmlWriter.Create(Response.Body, new XmlWriterSettings() { Async = true, Indent = true }))
            {
                var writer = await GetWriter(type, xmlWriter, lastUpdated());

                foreach (var thing in items)
                {
                    var item = serializer(host, thing);

                    await writer.Write(item);

                    await xmlWriter.FlushAsync();
                }

                await xmlWriter.FlushAsync();
            }

            await Response.Body.FlushAsync();
        }

        private AtomEntry SerializeComment(string host, Comment comment)
        {
            var item = new AtomEntry
            {
                Title = "comment",
                Description = comment.Content,
                Id = host + comment.ID,
                Published = comment.PubDate,
                LastUpdated = comment.PubDate,
                ContentType = "html",
            };

            item.AddContributor(new SyndicationPerson(comment.Author, comment.Email, "Contributor"));
            item.AddLink(new SyndicationLink(new Uri(item.Id)));
            return item;
        }

        private AtomEntry SerializePost(string host, Post post)
        {
            var item = new AtomEntry
            {
                Title = post.Title,
                Description = post.Content,
                Id = host + post.GetLink(),
                Published = post.PubDate,
                LastUpdated = post.LastModified,
                ContentType = "html",
            };

            foreach (string category in post.Categories)
            {
                item.AddCategory(new SyndicationCategory(category));
            }

            item.AddContributor(new SyndicationPerson(_settings.Value.Owner, email: "site@syfuhs.net"));
            item.AddLink(new SyndicationLink(new Uri(item.Id)));
            return item;
        }

        private async Task<ISyndicationFeedWriter> GetWriter(string type, XmlWriter xmlWriter, DateTime updated)
        {
            string host = Request.Scheme + "://" + Request.Host + "/";

            if (type.Equals("rss", StringComparison.OrdinalIgnoreCase))
            {
                var rss = new RssFeedWriter(xmlWriter);
                await rss.WriteTitle(_settings.Value.Name);
                await rss.WriteDescription(_settings.Value.Description);
                await rss.WriteGenerator("blog");
                await rss.WriteValue("link", host);
                return rss;
            }

            var atom = new AtomFeedWriter(xmlWriter);
            await atom.WriteTitle(_settings.Value.Name);
            await atom.WriteId(host);
            await atom.WriteSubtitle(_settings.Value.Description);
            await atom.WriteGenerator("blog", "https://github.com/stevesyfuhs/blog", "1.0");
            await atom.WriteValue("updated", updated.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            return atom;
        }
    }
}
