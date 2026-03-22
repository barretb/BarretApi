using Azure.Identity;
using Azure.Storage.Blobs;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.Services;

public sealed class AzureBlobScheduledPostImageStore : IScheduledPostImageStore
{
	private readonly BlobContainerClient _containerClient;
	private readonly ILogger<AzureBlobScheduledPostImageStore> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private bool _initialized;

	public AzureBlobScheduledPostImageStore(
		IOptions<ScheduledSocialPostOptions> options,
		ILogger<AzureBlobScheduledPostImageStore> logger)
	{
		_logger = logger;
		var opts = options.Value;
		var containerName = opts.BlobStorage.ContainerName;

		if (!string.IsNullOrWhiteSpace(opts.BlobStorage.ConnectionString))
		{
			_containerClient = new BlobContainerClient(opts.BlobStorage.ConnectionString, containerName);
		}
		else
		{
			var serviceClient = new BlobServiceClient(new Uri(opts.BlobStorage.AccountEndpoint), new DefaultAzureCredential());
			_containerClient = serviceClient.GetBlobContainerClient(containerName);
		}
	}

	public async Task<string> UploadAsync(
		string scheduledPostId,
		int imageIndex,
		byte[] content,
		string contentType,
		CancellationToken cancellationToken = default)
	{
		await EnsureInitializedAsync(cancellationToken);
		var blobName = $"{scheduledPostId}/{imageIndex:D2}";
		var blobClient = _containerClient.GetBlobClient(blobName);
		using var stream = new MemoryStream(content);
		await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);

		_logger.LogDebug("Uploaded scheduled post image blob {BlobName}", blobName);
		return blobName;
	}

	public async Task<byte[]> DownloadAsync(
		string blobName,
		CancellationToken cancellationToken = default)
	{
		var blobClient = _containerClient.GetBlobClient(blobName);
		var response = await blobClient.DownloadContentAsync(cancellationToken);
		return response.Value.Content.ToArray();
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		await _initLock.WaitAsync(cancellationToken);
		try
		{
			if (_initialized)
			{
				return;
			}

			await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
			_initialized = true;
			_logger.LogInformation("Ensured Azure Blob container {ContainerName} exists", _containerClient.Name);
		}
		finally
		{
			_initLock.Release();
		}
	}

}
