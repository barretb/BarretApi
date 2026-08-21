using System.Diagnostics.CodeAnalysis;
using Azure;
using Azure.Core;

namespace BarretApi.Infrastructure.UnitTests.TestSupport;

/// <summary>
/// A minimal, fully-formed <see cref="Response"/> for stubbing Azure Table SDK calls
/// (e.g. <c>TableClient.UpdateEntityAsync</c>) in unit tests. NSubstitute cannot fabricate
/// a usable <see cref="Response"/> for an unstubbed call - the proxy it returns has no
/// backing HTTP message, so reading <c>Headers.ETag</c> on it throws
/// <see cref="NullReferenceException"/>. Any test whose code path reads response headers
/// after an Azure Table write must stub that call to return a <see cref="FakeResponse"/>
/// instead of leaving it unconfigured.
/// </summary>
public sealed class FakeResponse : Response
{
    private readonly string? _quotedETag;

    /// <param name="eTag">
    /// The unquoted ETag value the caller expects back from <c>Headers.ETag</c> (e.g.
    /// <c>"etag-2"</c>). Azure's <see cref="ETag"/> parser requires the raw header value to
    /// be a quoted HTTP entity tag, so it is quoted here before being handed back through
    /// <see cref="TryGetHeader"/> - matching what real Table Storage sends on the wire.
    /// </param>
    public FakeResponse(string? eTag = null)
    {
        _quotedETag = eTag is null ? null : $"\"{eTag}\"";
    }

    public override int Status => 204;

    public override string ReasonPhrase => "No Content";

    public override Stream? ContentStream { get; set; }

    public override string ClientRequestId { get; set; } = string.Empty;

    public override void Dispose()
    {
    }

    protected override bool TryGetHeader(string name, [NotNullWhen(true)] out string? value)
    {
        if (_quotedETag is not null && name == HttpHeader.Names.ETag)
        {
            value = _quotedETag;
            return true;
        }

        value = null;
        return false;
    }

    protected override bool TryGetHeaderValues(string name, [NotNullWhen(true)] out IEnumerable<string>? values)
    {
        if (TryGetHeader(name, out var value))
        {
            values = [value!];
            return true;
        }

        values = null;
        return false;
    }

    protected override bool ContainsHeader(string name) => TryGetHeader(name, out _);

    protected override IEnumerable<HttpHeader> EnumerateHeaders()
    {
        if (_quotedETag is not null)
        {
            yield return new HttpHeader(HttpHeader.Names.ETag, _quotedETag);
        }
    }
}
