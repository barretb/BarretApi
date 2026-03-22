namespace BarretApi.Core.Interfaces;

public interface IScheduledPostImageStore
{
	Task<string> UploadAsync(
		string scheduledPostId,
		int imageIndex,
		byte[] content,
		string contentType,
		CancellationToken cancellationToken = default);

	Task<byte[]> DownloadAsync(
		string blobName,
		CancellationToken cancellationToken = default);
}
