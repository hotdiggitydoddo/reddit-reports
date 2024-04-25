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
			var tasks = new ConcurrentBag<Task>();
			while (true)
			{
				Console.WriteLine("Type the name of a subreddit to track, or just hit enter to exit:");
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

				if (e.AdditionalPostsAvailable) return;

				var topUser = subreddit.GetUserWithMostPosts();
				var topPost = subreddit.GetMostUpvotedPost();

				Console.WriteLine($"Totals for Subreddit \"{subreddit.Name}\"...");

				Console.WriteLine($"Top User: {topUser.Item1}");
				Console.WriteLine($"Post Count: {topUser.Item2}");
				Console.WriteLine("-=-=-=-=-=-=");
				Console.WriteLine($"Top Post: {topPost.Item1}");
				Console.WriteLine($"Upvote Count: {topPost.Item2}");
				Console.WriteLine();

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
