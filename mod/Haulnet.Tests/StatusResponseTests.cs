using System.Text;
using Xunit;

namespace Haulnet.Tests;

/// <summary>The raw HTTP answer of haulnet-server's <c>GET /status</c> and when the reader may stop reading.</summary>
public class StatusResponseTests
{
    private const string Body =
        "{\"haulnet_server\":\"0.1.1\",\"luxon\":\"v1.5\",\"name\":\"moscow-1\",\"rooms\":0,\"players\":0," +
        "\"max_rooms\":20,\"max_players\":120,\"cpu_busy\":0,\"accepting\":true,\"draining\":false,\"uptime_s\":12624}";

    // Headers as sent by haulnet-server 0.1.1 (moscow-1, 2026-11-14).
    private static string Answer(string body, int? contentLength = null, string status = "200 OK") =>
        $"HTTP/1.0 {status}\r\nCache-Control: no-store\r\nContent-Type: application/json\r\n" +
        "Date: Sat, 14 Nov 2026 13:53:29 GMT\r\n" +
        $"Content-Length: {contentLength ?? Encoding.UTF8.GetByteCount(body)}\r\n\r\n{body}";

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public void CompleteAnswerNeedsNoClose()
    {
        var data = Bytes(Answer(Body));

        Assert.True(StatusResponse.IsComplete(data, data.Length));
        Assert.Equal(Body, StatusResponse.Body(data, data.Length));
    }

    [Fact]
    public void AnswerIsCompleteInsideALargerBuffer()
    {
        var answer = Bytes(Answer(Body));
        var buffer = new byte[64 * 1024];
        answer.CopyTo(buffer, 0);

        Assert.True(StatusResponse.IsComplete(buffer, answer.Length));
        Assert.Equal(Body, StatusResponse.Body(buffer, answer.Length));
    }

    [Fact]
    public void PartialBodyIsNotComplete()
    {
        var data = Bytes(Answer(Body));
        var count = data.Length - 10;

        Assert.False(StatusResponse.IsComplete(data, count));
        Assert.Null(StatusResponse.Body(data, count));
    }

    [Fact]
    public void PartialHeadersAreNotComplete()
    {
        var data = Bytes(Answer(Body));
        var count = Answer(Body).IndexOf("\r\n\r\n", System.StringComparison.Ordinal);

        Assert.False(StatusResponse.IsComplete(data, count));
        Assert.Null(StatusResponse.Body(data, count));
    }

    [Fact]
    public void EmptyReadIsNotComplete()
    {
        Assert.False(StatusResponse.IsComplete(new byte[16], 0));
        Assert.Null(StatusResponse.Body(new byte[16], 0));
    }

    [Fact]
    public void ContentLengthCountsBytesNotCharacters()
    {
        var body = "{\"name\":\"Z" + (char)0xFC + "rich-1 " + (char)0x2014 + " caf" + (char)0xE9 + "\"}";
        var data = Bytes(Answer(body));

        Assert.True(StatusResponse.IsComplete(data, data.Length));
        Assert.False(StatusResponse.IsComplete(data, data.Length - 1));
        Assert.Equal(body, StatusResponse.Body(data, data.Length));
    }

    [Fact]
    public void BytesAfterTheBodyAreIgnored()
    {
        var data = Bytes(Answer(Body) + "trailing");

        Assert.True(StatusResponse.IsComplete(data, data.Length));
        Assert.Equal(Body, StatusResponse.Body(data, data.Length));
    }

    [Fact]
    public void HeaderNameIsCaseInsensitive()
    {
        var data = Bytes(Answer(Body).Replace("Content-Length:", "content-length:"));

        Assert.True(StatusResponse.IsComplete(data, data.Length));
    }

    [Fact]
    public void WithoutContentLengthOnlyTheCloseEndsTheAnswer()
    {
        var text = $"HTTP/1.0 200 OK\r\nContent-Type: application/json\r\n\r\n{Body}";
        var data = Bytes(text);

        Assert.False(StatusResponse.IsComplete(data, data.Length));
        Assert.Equal(Body, StatusResponse.Body(data, data.Length));
    }

    [Fact]
    public void InvalidContentLengthCountsAsAbsent()
    {
        var data = Bytes(Answer(Body).Replace($"Content-Length: {Encoding.UTF8.GetByteCount(Body)}", "Content-Length: -5"));

        Assert.False(StatusResponse.IsComplete(data, data.Length));
    }

    [Fact]
    public void EmptyBodyWithZeroLengthIsComplete()
    {
        var data = Bytes(Answer(string.Empty));

        Assert.True(StatusResponse.IsComplete(data, data.Length));
        Assert.Equal(string.Empty, StatusResponse.Body(data, data.Length));
    }

    [Theory]
    [InlineData("404 Not Found")]
    [InlineData("503 Service Unavailable")]
    public void OtherStatusHasNoBody(string status)
    {
        var data = Bytes(Answer("404 page not found\n", status: status));

        Assert.True(StatusResponse.IsComplete(data, data.Length));
        Assert.Null(StatusResponse.Body(data, data.Length));
    }

    [Fact]
    public void NonHttpAnswerHasNoBody()
    {
        var data = Bytes("SSH-2.0-OpenSSH_9.6\r\n\r\n");

        Assert.Null(StatusResponse.Body(data, data.Length));
    }
}
