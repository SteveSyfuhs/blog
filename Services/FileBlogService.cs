﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using blog.Models;
using FuzzySharp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace blog
{
    public class FileBlogService : IBlogService
    {
        private readonly List<Post> _cache = new List<Post>();
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly IConfiguration _config;
        private readonly string _folder;

        private readonly object _sync = new object();

        public FileBlogService(
            IWebHostEnvironment env,
            IHttpContextAccessor contextAccessor,
            IConfiguration config)
        {
            _folder = env != null ? Path.Combine(env.WebRootPath, "posts") : "";
            _contextAccessor = contextAccessor;
            _config = config;

            Site = config.GetSection("blog").Get<SiteSettings>();

            InitializeSync();
        }

        public BlogSettings Settings { get; private set; }

        protected SiteSettings Site { get; }

        public Task<int> GetPostCount(bool includePages)
        {
            return Task.FromResult(PostsWhere(includePages).Count());
        }

        protected void InitializeSync()
        {
            var mre = new ManualResetEvent(false);

            Initialize().ContinueWith(t =>
            {
                mre.Set();
            });

            mre.WaitOne(TimeSpan.FromSeconds(90));
        }

        public virtual Task<ImagesModel> ListImages()
        {
            var result = new ImagesModel();

            return Task.FromResult(result);
        }

        public virtual Task<IEnumerable<Post>> GetAllContent(int count, int skip = 0)
        {
            var posts = PostsWhere(includePages: true)
                   .Skip(skip)
                   .Take(count);

            return Task.FromResult(posts);
        }

        public virtual Task UpdateSettings(BlogSettings localSettings)
        {
            return Task.CompletedTask;
        }

        public virtual Task<IEnumerable<Post>> GetPosts(int count, int skip = 0)
        {
            var posts = PostsWhere()
                .Skip(skip)
                .Take(count);

            return Task.FromResult(posts);
        }

        public virtual Task<IEnumerable<Post>> GetDraftPosts()
        {
            var posts = PostsWhere(includePages: true)
                            .Where(p => !string.IsNullOrWhiteSpace(p.Draft));

            return Task.FromResult(posts);
        }

        private IEnumerable<Post> PostsWhere(bool includePages = false)
        {
            bool isAdmin = IsAdmin();

            return _cache
                    .Where(p => p.Type == PostType.Post || includePages)
                    .Where(p => p.PubDate <= DateTime.UtcNow || isAdmin)
                    .Where(p => (p.IsIndexed && p.IsPublished) || isAdmin);
        }

        public virtual Task<IEnumerable<Post>> GetPostsByCategory(string category)
        {
            var posts = PostsWhere()
                .Where(p => p.Categories.Contains(category, StringComparer.OrdinalIgnoreCase));

            return Task.FromResult(posts);

        }

        public Task<IEnumerable<Post>> GetPostsByMonth(int year, int month)
        {
            var posts = PostsWhere()
                .Where(p => p.PubDate.Year == year)
                .Where(p => p.PubDate.Month == month);

            return Task.FromResult(posts);
        }

        public virtual Task<Post> GetPostBySlug(string slug)
        {
            var post = PostsWhere(includePages: true).FirstOrDefault(p => SlugEquals(slug, p));

            if (post != null)
            {
                post.Author = GetAuthor();

                return Task.FromResult(post);
            }

            return Task.FromResult<Post>(null);
        }

        public Author GetAuthor()
        {
            var authorPost = _cache.FirstOrDefault(c => SlugEquals("author-footer", c) && c.Type == PostType.Page && c.IsPublished);

            if (authorPost == null)
            {
                return null;
            }

            return new Author
            {
                Name = authorPost.Title,
                Description = authorPost.Content,
                ImageUrl = authorPost.PrimaryMediaUrl
            };
        }

        private static bool SlugEquals(string slug, Post p)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return false;
            }

            var searchingSlug = slug;

            if (searchingSlug.EndsWith('/'))
            {
                searchingSlug = searchingSlug[0..^1];
            }

            var postSlug = p.Slug;

            if (string.IsNullOrWhiteSpace(postSlug))
            {
                return false;
            }

            if (postSlug.EndsWith('/'))
            {
                postSlug = postSlug[0..^1];
            }

            return postSlug.Equals(searchingSlug, StringComparison.OrdinalIgnoreCase);
        }

        public virtual Task<Post> GetPostById(string id)
        {
            var post = PostsWhere(includePages: true).FirstOrDefault(p => p.ID.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (post != null)
            {
                return Task.FromResult(post);
            }

            return Task.FromResult<Post>(null);
        }

        public virtual Task<IEnumerable<(string Category, int Count)>> GetCategories()
        {
            bool isAdmin = IsAdmin();

            var categories = _cache
                .Where(p => p.Type == PostType.Post)
                .Where(p => p.IsPublished || isAdmin)
                .SelectMany(post => post.Categories)
                .Select(cat => new { Normal = cat, Lower = cat.ToLowerInvariant() })
                .GroupBy(c => c.Lower)
                .OrderByDescending(c => c.Count())
                .ThenBy(c => c.Key)
                .Select(s => (s.First().Normal, s.Count()));

            return Task.FromResult(categories);
        }

        public async Task SavePost(Post post)
        {
            await SaveFilesToDisk(post);

            post.LastModified = DateTime.UtcNow;

            XDocument doc = new XDocument(
                            new XElement("post",
                                new XElement("title", post.Title),
                                new XElement("slug", post.Slug),
                                new XElement("pubDate", post.PubDate.ToString("yyyy-MM-dd HH:mm:ss")),
                                new XElement("lastModified", post.LastModified.ToString("yyyy-MM-dd HH:mm:ss")),
                                new XElement("mediaUrl", post.PrimaryMediaUrl),
                                new XElement("heroImageUrl", post.HeroBackgroundImageUrl),
                                new XElement("postType", post.Type),
                                new XElement("excerpt", post.Excerpt),
                                new XElement("content", post.Content),
                                new XElement("ispublished", post.IsPublished),
                                new XElement("categories", string.Empty),
                                new XElement("comments", string.Empty),
                                new XElement("includeAuthor", post.IncludeAuthor),
                                new XElement("isIndexed", post.IsIndexed),
                                new XElement("lastDraft", post.Draft)
                            ));

            XElement categories = doc.XPathSelectElement("post/categories");
            foreach (string category in post.Categories)
            {
                categories.Add(new XElement("category", category));
            }

            XElement comments = doc.XPathSelectElement("post/comments");
            foreach (Comment comment in post.Comments)
            {
                comments.Add(
                    new XElement("comment",
                        new XElement("author", comment.Author),
                        new XElement("email", comment.Email),
                        new XElement("date", comment.PubDate.ToString("yyyy-MM-dd HH:m:ss")),
                        new XElement("content", comment.Content),
                        new XAttribute("isAdmin", comment.IsAdmin),
                        new XAttribute("id", comment.ID)
                    ));
            }

            await PersistPost(post, doc);

            await Initialize();
        }

        protected virtual async Task PersistPost(Post post, XDocument doc)
        {
            string filePath = GetFilePath(post);

            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite))
            {
                await doc.SaveAsync(fs, SaveOptions.None, CancellationToken.None).ConfigureAwait(false);
            }
        }

        public virtual Task DeletePost(Post post)
        {
            string filePath = GetFilePath(post);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            if (_cache.Contains(post))
            {
                _cache.Remove(post);
            }

            return Task.CompletedTask;
        }

        public async Task<string> SaveFile(byte[] bytes, string fileName, string suffix = null)
        {
            suffix ??= DateTime.UtcNow.Ticks.ToString();

            return await PersistDataFile(bytes, fileName, suffix);
        }

        protected virtual async Task<string> PersistDataFile(byte[] bytes, string fileName, string suffix)
        {
            string ext = Path.GetExtension(fileName);
            string name = Path.GetFileNameWithoutExtension(fileName);

            string relative = $"files/{name}_{suffix}{ext}";
            string absolute = Path.Combine(_folder, relative);
            string dir = Path.GetDirectoryName(absolute);

            Directory.CreateDirectory(dir);

            using (var writer = new FileStream(absolute, FileMode.CreateNew))
            {
                await writer.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            }

            return "/posts/" + relative;
        }

        private string GetFilePath(Post post)
        {
            return Path.Combine(_folder, post.ID + ".xml");
        }

        protected async Task Initialize()
        {
            Settings = await LoadSettings();

            if (Settings == null)
            {
                throw new InvalidOperationException("Settings are null");
            }

            var posts = await LoadPosts();

            lock (_sync)
            {
                _cache.Clear();

                foreach (var post in posts)
                {
                    _cache.Add(post);
                }

                SortCache();
            }
        }

        protected virtual Task<BlogSettings> LoadSettings()
        {
            var settings = _config.GetSection("blog").Get<BlogSettings>();

            return Task.FromResult(settings);
        }

        protected virtual Task<IEnumerable<Post>> LoadPosts()
        {
            if (!Directory.Exists(_folder))
            {
                Directory.CreateDirectory(_folder);
            }

            var posts = new List<Post>();

            foreach (string file in Directory.EnumerateFiles(_folder, "*.xml", SearchOption.TopDirectoryOnly))
            {
                var stream = File.OpenRead(file);

                LoadPost(file, stream);
            }

            return Task.FromResult(posts.AsEnumerable());
        }

        protected Post LoadPost(string file, Stream stream)
        {
            // stream.Seek(0, SeekOrigin.Begin);

            XElement doc = XElement.Load(stream);

            Post post = new Post(this)
            {
                ID = Path.GetFileNameWithoutExtension(file),
                Title = ReadValue(doc, "title"),
                Excerpt = ReadValue(doc, "excerpt"),
                PrimaryMediaUrl = ReadValue(doc, "mediaUrl"),
                HeroBackgroundImageUrl = ReadValue(doc, "heroImageUrl"),
                Type = Enum.Parse<PostType>(ReadValue(doc, "postType", "Post"), true),
                Content = ReadValue(doc, "content"),
                Slug = ReadValue(doc, "slug").ToLowerInvariant(),
                PubDate = DateTime.Parse(ReadValue(doc, "pubDate")),
                LastModified = DateTime.Parse(ReadValue(doc, "lastModified", DateTime.Now.ToString())),
                IsPublished = bool.Parse(ReadValue(doc, "ispublished", "true")),
                IncludeAuthor = bool.Parse(ReadValue(doc, "includeAuthor", "true")),
                IsIndexed = bool.Parse(ReadValue(doc, "isIndexed", "true")),
                Draft = ReadValue(doc, "lastDraft", null)
            };

            LoadCategories(post, doc);
            LoadComments(post, doc);

            return post;
        }

        private static void LoadCategories(Post post, XElement doc)
        {
            XElement categories = doc.Element("categories");
            if (categories == null)
                return;

            List<string> list = new List<string>();

            foreach (var node in categories.Elements("category"))
            {
                list.Add(node.Value);
            }

            post.Categories = list.ToArray();
        }

        private static void LoadComments(Post post, XElement doc)
        {
            var comments = doc.Element("comments");

            if (comments == null)
                return;

            foreach (var node in comments.Elements("comment"))
            {
                Comment comment = new Comment()
                {
                    ID = ReadAttribute(node, "id"),
                    Author = ReadValue(node, "author"),
                    Email = ReadValue(node, "email"),
                    IsAdmin = bool.Parse(ReadAttribute(node, "isAdmin", "false")),
                    Content = ReadValue(node, "content"),
                    PubDate = DateTime.Parse(ReadValue(node, "date", "2000-01-01")),
                };

                post.Comments.Add(comment);
            }
        }

        private static string ReadValue(XElement doc, XName name, string defaultValue = "")
        {
            if (doc.Element(name) != null)
            {
                return doc.Element(name).Value;
            }

            return defaultValue;
        }

        private static string ReadAttribute(XElement element, XName name, string defaultValue = "")
        {
            if (element.Attribute(name) != null)
            {
                return element.Attribute(name).Value;
            }

            return defaultValue;
        }

        protected void SortCache()
        {
            _cache.Sort((p1, p2) => p2.PubDate.CompareTo(p1.PubDate));
        }

        protected bool IsAdmin()
        {
            return _contextAccessor.HttpContext?.User?.Identity.IsAuthenticated == true;
        }

        private async Task SaveFilesToDisk(Post post)
        {
            var imgRegex = new Regex("<img[^>].+ />", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var base64Regex = new Regex("data:[^/]+/(?<ext>[a-z]+);base64,(?<base64>.+)", RegexOptions.IgnoreCase);

            foreach (Match match in imgRegex.Matches(post.Content))
            {
                try
                {
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml("<root>" + match.Value + "</root>");

                    var img = doc.FirstChild.FirstChild;
                    var srcNode = img.Attributes["src"];
                    var fileNameNode = img.Attributes["data-filename"];

                    // The HTML editor creates base64 DataURIs which we'll have to convert to image files on disk
                    if (srcNode != null && fileNameNode != null)
                    {
                        var base64Match = base64Regex.Match(srcNode.Value);
                        if (base64Match.Success)
                        {
                            byte[] bytes = Convert.FromBase64String(base64Match.Groups["base64"].Value);
                            srcNode.Value = await SaveFile(bytes, fileNameNode.Value).ConfigureAwait(false);

                            img.Attributes.Remove(fileNameNode);
                            post.Content = post.Content.Replace(match.Value, img.OuterXml);
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }
        }

        public Task<SearchResults> Search(string q, int skip, int take)
        {
            var isAdmin = IsAdmin();

            var words = SplitWords(q);

            var searchResults = new List<KeyValuePair<int, Post>>();

            bool searchable = words.Any();

            if (searchable)
            {
                foreach (var post in PostsWhere(includePages: true))
                {
                    var count = 0;

                    if (Fuzz.PartialRatio(post.Slug, q) >= 90)
                    {
                        count += 10000;
                    }

                    count += Fuzz.PartialTokenSortRatio(post.Title, q);

                    var content = SplitWords(post.Content);

                    foreach (var qWord in words)
                    {
                        var extracted = Process.ExtractSorted(qWord, content);

                        count += extracted.Sum(e => e.Score > 75 ? e.Score : 0);
                    }

                    if (count > 100)
                    {
                        searchResults.Add(KeyValuePair.Create(count, post));
                    }
                }
            }

            var results = searchResults.OrderByDescending(r => r.Key).Where(r => r.Key > 0);

            if (!searchable)
            {
                results = _cache.Where(p => p.IsPublished || isAdmin).Select(c => KeyValuePair.Create(0, c));
            }

            var total = results.Count();

            results = results.Skip(skip).Take(take);

            var searchResult = new SearchResults
            {
                Query = q,
                Results = total,
                Words = words,
                Posts = results
            };

            return Task.FromResult(searchResult);
        }

        private static readonly char[] WordSplits = new[]
        {
            ' ', ',', ';', ':', '=', '+', '-', '<', '>', '/', '\\', '&', '.', '!', '?', '\r', '\n',
            '[', ']', '{', '}', '|', '(', ')'
        };

        private static IEnumerable<string> SplitWords(string q)
        {
            return q.ToLowerInvariant().Split(WordSplits, StringSplitOptions.RemoveEmptyEntries).Except(StopWords).Where(s => s.Length > 2);
        }

        public virtual Task DeleteFile(string file)
        {
            return Task.CompletedTask;
        }

        private static readonly string[] StopWords = new[]
        {
            "a",
            "about",
            "above",
            "above",
            "across",
            "after",
            "afterwards",
            "again",
            "against",
            "all",
            "almost",
            "alone",
            "along",
            "already",
            "also",
            "although",
            "always",
            "am",
            "among",
            "amongst",
            "amoungst",
            "amount",
            "an",
            "and",
            "another",
            "any",
            "anyhow",
            "anyone",
            "anything",
            "anyway",
            "anywhere",
            "are",
            "around",
            "as",
            "at",
            "back",
            "be",
            "became",
            "because",
            "become",
            "becomes",
            "becoming",
            "been",
            "before",
            "beforehand",
            "behind",
            "being",
            "below",
            "beside",
            "besides",
            "between",
            "beyond",
            "both",
            "bottom",
            "but",
            "by",
            "call",
            "can",
            "cannot",
            "cant",
            "co",
            "con",
            "could",
            "couldnt",
            "cry",
            "de",
            "describe",
            "detail",
            "do",
            "done",
            "down",
            "due",
            "during",
            "each",
            "eg",
            "eight",
            "either",
            "eleven",
            "else",
            "elsewhere",
            "empty",
            "enough",
            "etc",
            "even",
            "ever",
            "every",
            "everyone",
            "everything",
            "everywhere",
            "except",
            "few",
            "fifteen",
            "fify",
            "fill",
            "find",
            "fire",
            "first",
            "five",
            "for",
            "former",
            "formerly",
            "forty",
            "found",
            "four",
            "from",
            "front",
            "full",
            "further",
            "get",
            "give",
            "go",
            "had",
            "has",
            "hasnt",
            "have",
            "he",
            "hence",
            "her",
            "here",
            "hereafter",
            "hereby",
            "herein",
            "hereupon",
            "hers",
            "herself",
            "him",
            "himself",
            "his",
            "how",
            "however",
            "hundred",
            "ie",
            "if",
            "in",
            "inc",
            "indeed",
            "interest",
            "into",
            "is",
            "it",
            "its",
            "itself",
            "keep",
            "last",
            "latter",
            "latterly",
            "least",
            "less",
            "ltd",
            "made",
            "many",
            "may",
            "me",
            "meanwhile",
            "might",
            "mill",
            "mine",
            "more",
            "moreover",
            "most",
            "mostly",
            "move",
            "much",
            "must",
            "my",
            "myself",
            "name",
            "namely",
            "neither",
            "never",
            "nevertheless",
            "next",
            "nine",
            "no",
            "nobody",
            "none",
            "noone",
            "nor",
            "not",
            "nothing",
            "now",
            "nowhere",
            "of",
            "off",
            "often",
            "on",
            "once",
            "one",
            "only",
            "onto",
            "or",
            "other",
            "others",
            "otherwise",
            "our",
            "ours",
            "ourselves",
            "out",
            "over",
            "own",
            "part",
            "per",
            "perhaps",
            "please",
            "put",
            "rather",
            "re",
            "same",
            "see",
            "seem",
            "seemed",
            "seeming",
            "seems",
            "serious",
            "several",
            "she",
            "should",
            "show",
            "side",
            "since",
            "sincere",
            "six",
            "sixty",
            "so",
            "some",
            "somehow",
            "someone",
            "something",
            "sometime",
            "sometimes",
            "somewhere",
            "still",
            "such",
            "system",
            "take",
            "ten",
            "than",
            "that",
            "the",
            "their",
            "them",
            "themselves",
            "then",
            "thence",
            "there",
            "thereafter",
            "thereby",
            "therefore",
            "therein",
            "thereupon",
            "these",
            "they",
            "thickv",
            "thin",
            "third",
            "this",
            "those",
            "though",
            "three",
            "through",
            "throughout",
            "thru",
            "thus",
            "to",
            "together",
            "too",
            "top",
            "toward",
            "towards",
            "twelve",
            "twenty",
            "two",
            "un",
            "under",
            "until",
            "up",
            "upon",
            "us",
            "very",
            "via",
            "was",
            "we",
            "well",
            "were",
            "what",
            "whatever",
            "when",
            "whence",
            "whenever",
            "where",
            "whereafter",
            "whereas",
            "whereby",
            "wherein",
            "whereupon",
            "wherever",
            "whether",
            "which",
            "while",
            "whither",
            "who",
            "whoever",
            "whole",
            "whom",
            "whose",
            "why",
            "will",
            "with",
            "within",
            "without",
            "would"
        };
    }
}
