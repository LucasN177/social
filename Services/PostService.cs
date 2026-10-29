using Social.Core.Interfaces.Services;
using Social.Core.Models.Database;

namespace Social.Services;

public class PostService : IPostService
{
        private readonly List<Post> _posts;

        public PostService()
        {
            _posts = GenerateMockPosts();
        }

        public async Task<List<Post>> GetFeedPostsAsync()
        {
            await Task.Delay(500); // Simulate API call
            return _posts.OrderByDescending(p => p.CreatedAt).ToList();
        }

        public async Task<Post> GetPostByIdAsync(int id)
        {
            await Task.Delay(300);
            return _posts.FirstOrDefault(p => p.Id == id) ?? new Post();
        }

        public async Task<bool> ToggleLikeAsync(int postId)
        {
            await Task.Delay(200);
            var post = _posts.FirstOrDefault(p => p.Id == postId);
            if (post != null)
            {
                post.IsLiked = !post.IsLiked;
                post.LikesCount += post.IsLiked ? 1 : -1;
                return true;
            }
            return false;
        }

        public async Task<bool> ToggleBookmarkAsync(int postId)
        {
            await Task.Delay(200);
            var post = _posts.FirstOrDefault(p => p.Id == postId);
            if (post != null)
            {
                post.IsBookmarked = !post.IsBookmarked;
                return true;
            }
            return false;
        }

        public async Task<List<Comment>> GetCommentsAsync(int postId)
        {
            await Task.Delay(300);
            return new List<Comment>
            {
                new Comment
                {
                    Id = 1,
                    User = new User { Username = "jane_doe", DisplayName = "Jane Doe", ProfileImageUrl = "https://via.placeholder.com/40x40" },
                    Content = "Wow, this is amazing! 🔥",
                    LikesCount = 12,
                    CreatedAt = DateTime.Now.AddMinutes(-30)
                },
                new Comment
                {
                    Id = 2,
                    User = new User { Username = "mike_smith", DisplayName = "Mike Smith", ProfileImageUrl = "https://via.placeholder.com/40x40" },
                    Content = "Great shot! Where was this taken?",
                    LikesCount = 8,
                    CreatedAt = DateTime.Now.AddMinutes(-15)
                }
            };
        }

        public async Task<Comment> AddCommentAsync(int postId, string content)
        {
            await Task.Delay(400);
            return new Comment
            {
                Id = new Random().Next(1000),
                User = new User { Username = "current_user", DisplayName = "You", ProfileImageUrl = "https://via.placeholder.com/40x40" },
                Content = content,
                LikesCount = 0,
                CreatedAt = DateTime.Now
            };
        }

        private List<Post> GenerateMockPosts()
        {
            return new List<Post>
            {
                new Post
                {
                    Id = 1,
                    User = new User { Username = "john_doe", DisplayName = "John Doe", ProfileImageUrl = "https://via.placeholder.com/40x40", IsVerified = true },
                    Content = "Beautiful sunset at the beach today! 🌅 #sunset #beach #nature",
                    ImageUrls = new List<string> { "https://via.placeholder.com/600x400" },
                    LikesCount = 234,
                    CommentsCount = 12,
                    SharesCount = 5,
                    CreatedAt = DateTime.Now.AddHours(-2),
                    Location = "Malibu Beach",
                    Tags = new List<string> { "sunset", "beach", "nature" }
                },
                new Post
                {
                    Id = 2,
                    User = new User { Username = "sarah_wilson", DisplayName = "Sarah Wilson", ProfileImageUrl = "https://via.placeholder.com/40x40" },
                    Content = "Coffee and code - perfect morning combo! ☕️💻",
                    ImageUrls = new List<string> { "https://via.placeholder.com/600x400" },
                    LikesCount = 89,
                    CommentsCount = 7,
                    SharesCount = 2,
                    CreatedAt = DateTime.Now.AddHours(-4),
                    Location = "Downtown Café",
                    Tags = new List<string> { "coffee", "coding", "morning" }
                },
                new Post
                {
                    Id = 3,
                    User = new User { Username = "travel_explorer", DisplayName = "Travel Explorer", ProfileImageUrl = "https://via.placeholder.com/40x40", IsVerified = true },
                    Content = "Exploring the mountains of Switzerland 🏔️ What an incredible view!",
                    ImageUrls = new List<string> { "https://via.placeholder.com/600x400", "https://via.placeholder.com/600x400" },
                    LikesCount = 567,
                    CommentsCount = 34,
                    SharesCount = 15,
                    CreatedAt = DateTime.Now.AddHours(-6),
                    Location = "Swiss Alps",
                    Tags = new List<string> { "travel", "mountains", "switzerland" }
                }
            };
        }
}