using blog.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace blog
{
    public interface IBlogService
    {
        BlogSettings Settings { get; }

        Task<int> GetPostCount(bool includePages);

        Task<IEnumerable<Post>> GetAllContent(int count, int skip = 0);

        Task<IEnumerable<Post>> GetPosts(int count, int skip = 0);

        Task<IEnumerable<Post>> GetPostsByCategory(string category);

        Task<IEnumerable<Post>> GetPostsByMonth(int year, int month);

        Task<Post> GetPostBySlug(string slug);

        Task<Post> GetPostById(string id);

        Task<IEnumerable<Post>> GetDraftPosts();

        Author GetAuthor();

        Task<IEnumerable<(string Category, int Count)>> GetCategories();

        Task SavePost(Post post);

        Task DeletePost(Post post);

        Task<string> SaveFile(byte[] bytes, string fileName, string suffix = null);

        Task<SearchResults> Search(string q, int skip, int take);

        Task<ImagesModel> ListImages();
        
        Task DeleteFile(string file);

        Task UpdateSettings(BlogSettings localSettings);
    }
}
