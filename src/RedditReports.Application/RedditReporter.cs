using System.Collections.Concurrent;
using RedditReports.Application.Abstractions;
using RedditReports.Domain.Abstractions;

namespace RedditReports.Application
{
	public class RedditReporterService : IRedditReporterService, IDisposable
	{
		private readonly IRedditApiClient _redditApiClient;

		private readonly ConcurrentDictionary<string, int> _mostUpvotedPosts = new();
		private readonly ConcurrentDictionary<string, int> _mostActiveUsers = new();

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
				Console.WriteLine("Type the name of a subreddit to track:");
				var subreddit = Console.ReadLine();
				if (subreddit == null)
				{
					break;
				}

				var task = Task.Run(() => { _redditApiClient.FetchAsync(subreddit); });
				tasks.Add(task);
			}
			//	//// Fetch a smaller chunk of new posts
			//var task1 = _redditApiClient.FetchAsync("music");
			//var task2 = _redditApiClient.FetchAsync("funny");
			//var task3 = _redditApiClient.FetchAsync("jokes");
			//var task4 = _redditApiClient.FetchAsync("worldnews");
			//var task5 = _redditApiClient.FetchAsync("patientgamers");

			//await Task.WhenAll(task1, task2, task3, task4, task5);
			//await _redditApiClient.FetchAsync("patientgamers");
			await Task.WhenAll(tasks);
		}

		private void OnRedditDataReceived(object? sender, RedditDataReceivedEventArgs e)
		{
			foreach (var post in e.Posts)
			{
				//Console.WriteLine("New Post by " + post.Author + ": " + post.Title);
				_mostActiveUsers.AddOrUpdate(post.Author, 1, (author, currentPosts) => currentPosts += 1);
				_mostUpvotedPosts.AddOrUpdate(post.Title, post.Ups, (author, upvoteCount) => upvoteCount += post.Ups);
			}
			var topPoster = _mostActiveUsers.MaxBy(x => x.Value);
			var mostUpvotes = _mostUpvotedPosts.MaxBy(x => x.Value);
			var subreddit = e.Posts.First().Subreddit;

			Console.WriteLine($"Update for Subreddit \"{subreddit}\"...");

			Console.WriteLine($"Top User: {topPoster.Key}");
			Console.WriteLine($"Post Count: {topPoster.Value}");
			Console.WriteLine("-=-=-=-=-=-=");
			Console.WriteLine($"Top Post: {mostUpvotes.Key}");
			Console.WriteLine($"Upvote Count: {mostUpvotes.Value}");
			Console.WriteLine();
		}

		public void Dispose()
		{
			_redditApiClient.RedditDataReceived -= OnRedditDataReceived;
		}
	}
}
