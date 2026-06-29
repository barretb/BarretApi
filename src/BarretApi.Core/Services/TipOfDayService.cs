using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Core.Services;

public sealed class TipOfDayService(
	ITipOfDayRepository tipOfDayRepository,
	SocialPostService socialPostService,
	IOptions<TipOfDayOptions> tipOfDayOptions,
	ILogger<TipOfDayService> logger)
{
	private readonly ITipOfDayRepository _tipOfDayRepository = tipOfDayRepository;
	private readonly SocialPostService _socialPostService = socialPostService;
	private readonly TipOfDayOptions _options = tipOfDayOptions.Value;
	private readonly ILogger<TipOfDayService> _logger = logger;

	public async Task<TipOfDayPostResult> SelectAndPostAsync(
		TipOfDayPostCommand command,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(command.Category);

		var now = DateTimeOffset.UtcNow;
		var cutoff = now.AddDays(-_options.RepostCooldownDays);
		var eligibleTips = await _tipOfDayRepository.GetEligibleByCategoryAsync(
			command.Category,
			cutoff,
			cancellationToken);

		if (eligibleTips.Count == 0)
		{
			_logger.LogWarning(
				"No eligible tip-of-the-day records found for category {Category}",
				command.Category);
			throw new InvalidOperationException("No eligible tips found for the requested category.");
		}

		var selectedTip = eligibleTips[Random.Shared.Next(eligibleTips.Count)];
		var post = new SocialPost
		{
			Text = BuildPostText(selectedTip, command.Leader),
			TargetPlatforms = command.Platforms
		};

		_logger.LogInformation(
			"Selected tip {TipId} for category {Category}",
			selectedTip.TipId,
			selectedTip.Category);

		var platformResults = await _socialPostService.PostAsync(post, cancellationToken);
		var hasSuccessfulPost = platformResults.Any(r => r.Success);

		if (hasSuccessfulPost)
		{
			await _tipOfDayRepository.MarkPostedAsync(selectedTip.TipId, now, cancellationToken);
		}

		return new TipOfDayPostResult
		{
			SelectedTip = selectedTip,
			PlatformResults = platformResults,
			TipMarkedPosted = hasSuccessfulPost,
			AttemptedAtUtc = now
		};
	}

	public async Task<TipOfDayRecord> AddTipAsync(
		string category,
		string tip,
		string? moreInfoUrl,
		CancellationToken cancellationToken = default)
	{
		var records = await AddTipsAsync(category, [(tip, moreInfoUrl)], cancellationToken);
		return records[0];
	}

	public async Task<IReadOnlyList<TipOfDayRecord>> AddTipsAsync(
		string category,
		IEnumerable<(string Tip, string? MoreInfoUrl)> tips,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(category);
		ArgumentNullException.ThrowIfNull(tips);

		var records = new List<TipOfDayRecord>();
		foreach (var tip in tips)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(tip.Tip);

			records.Add(new TipOfDayRecord
			{
				TipId = Guid.NewGuid().ToString("N"),
				Category = category.Trim(),
				Tip = tip.Tip.Trim(),
				MoreInfoUrl = string.IsNullOrWhiteSpace(tip.MoreInfoUrl) ? null : tip.MoreInfoUrl.Trim(),
				LastPostedDate = null,
				CreatedAtUtc = DateTimeOffset.UtcNow
			});
		}

		if (records.Count == 0)
		{
			throw new ArgumentException("At least one tip is required.", nameof(tips));
		}

		foreach (var record in records)
		{
			await _tipOfDayRepository.AddAsync(record, cancellationToken);
		}

		return records;
	}

	private static string BuildPostText(TipOfDayRecord tip, string? leader)
	{
		var parts = new List<string>();

		if (!string.IsNullOrWhiteSpace(leader))
		{
			parts.Add(leader.Trim());
		}

		parts.Add(tip.Tip);

		if (!string.IsNullOrWhiteSpace(tip.MoreInfoUrl))
		{
			parts.Add(tip.MoreInfoUrl);
		}

		return string.Join('\n', parts);
	}
}
