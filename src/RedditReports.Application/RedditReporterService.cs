using System.Collections.Concurrent;
using RedditReports.Application.Abstractions;
using RedditReports.Domain.Abstractions;
using RedditReports.Domain.Models;

namespace RedditReports.Application
{
	

	public class RedditReporterService : IRedditReporterService, IDisposable
	{
		private readonly IRedditApiClient _redditApiClient;
		private readonly ConcurrentDictionary<string, Subreddit> _subreddits = new();

		public RedditReporterService(IRedditApiClient redditApiClient)
		{
			_redditApiClient = redditApiClient;
			_redditApiClient.RedditDataReceived += OnRedditDataReceived;
		}

		public async Task GoAsync()
		{
			var authResult = await _redditApiClient.AuthenticateAsync();
			if (authResult == null || !authResult.Success) 
			{
				return;
			}

			var tasks = new ConcurrentBag<Task>();
			while (true)
			{
				Console.ForegroundColor = ConsoleColor.Blue;
				Console.WriteLine("Type the name of a subreddit to track, or just hit enter to exit:");
				Console.ForegroundColor = ConsoleColor.Gray;
				var subreddit = Console.ReadLine();
				if (string.IsNullOrWhiteSpace(subreddit))
				{
					break;
				}
				var task = Task.Run(() => { _redditApiClient.FetchAsync(subreddit); });
				tasks.Add(task);
			}
			await Task.WhenAll(tasks);
		}

		private void OnRedditDataReceived(object? sender, RedditDataReceivedEventArgs e)
		{
			try
			{
				var subreddit = _subreddits.AddOrUpdate(e.SubredditName, new Subreddit(e.SubredditName, e.Posts), (x, y) =>
				{
					y.Posts.AddRange(e.Posts);
					return y;
				});

				if (e.AdditionalPostsAvailable || subreddit.Posts.Count == 0) return;

				var topUser = subreddit.GetUserWithMostPosts();
				var topPost = subreddit.GetMostUpvotedPost();

				Console.ForegroundColor = ConsoleColor.DarkYellow;
				Console.Write($"\nTotals for Subreddit \"");
				Console.ForegroundColor = ConsoleColor.Gray;
				Console.Write(subreddit.Name);
				Console.ForegroundColor = ConsoleColor.DarkYellow;
				Console.WriteLine($"\"");

				Console.ForegroundColor = ConsoleColor.DarkYellow;
				Console.Write("Top User: ");
				Console.ForegroundColor = ConsoleColor.Gray;
				Console.WriteLine($"{topUser.Item1}");
				Console.ForegroundColor = ConsoleColor.DarkYellow;
				Console.Write("Post Count: ");
				Console.ForegroundColor = ConsoleColor.Gray;
				Console.WriteLine($"{topUser.Item2}");
				Console.ForegroundColor = ConsoleColor.DarkGray;
				Console.WriteLine("-=-=-=-=-=-=");
				Console.ForegroundColor = ConsoleColor.DarkYellow;
				Console.Write($"Top Post: ");
				Console.ForegroundColor = ConsoleColor.Gray;
				Console.WriteLine($"{topPost.Item1}");
				Console.ForegroundColor = ConsoleColor.DarkYellow;
				Console.Write($"Upvote Count: ");
				Console.ForegroundColor = ConsoleColor.Gray;
				Console.WriteLine($"{topPost.Item2}");
				Console.WriteLine();
				Console.ForegroundColor = ConsoleColor.Gray;
				_subreddits.TryRemove(subreddit.Name, out _);
			}
			catch (Exception ex)
			{

				throw;
			}
			
		}

		public void Dispose()
		{
			_redditApiClient.RedditDataReceived -= OnRedditDataReceived;
		}
	}
}
