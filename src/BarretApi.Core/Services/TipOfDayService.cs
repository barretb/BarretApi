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
		ArgumentException.ThrowIfNullOrWhiteSpace(category);
		ArgumentException.ThrowIfNullOrWhiteSpace(tip);

		var record = new TipOfDayRecord
		{
			TipId = Guid.NewGuid().ToString("N"),
			Category = category.Trim(),
			Tip = tip.Trim(),
			MoreInfoUrl = string.IsNullOrWhiteSpace(moreInfoUrl) ? null : moreInfoUrl.Trim(),
			LastPostedDate = null,
			CreatedAtUtc = DateTimeOffset.UtcNow
		};

		await _tipOfDayRepository.AddAsync(record, cancellationToken);
		return record;
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
