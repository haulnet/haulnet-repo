using System;
using System.Globalization;
using System.Text;

namespace Haulnet;

/// <summary>
/// The HTTP/1.0 answer of haulnet-server's status endpoint, read from raw bytes. The response counts as complete
/// once the headers and <c>Content-Length</c> bytes of body have arrived, so the reader does not have to wait for
/// the server to close the connection: some proxying VPN clients deliver the answer at once but pass the close on
/// seconds later. Kept free of game types so it can be tested without the game.
/// </summary>
internal static class StatusResponse
{
    private static readonly byte[] HeaderEnd = { (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };

    /// <summary>
    /// True when the first <paramref name="count"/> bytes hold the headers and the whole body announced by
    /// <c>Content-Length</c>. Without that header only the end of the connection marks the end of the body.
    /// </summary>
    public static bool IsComplete(byte[] data, int count)
    {
        var headerLength = FindHeaderEnd(data, count);
        if (headerLength < 0)
        {
            return false;
        }

        var contentLength = ContentLength(data, headerLength);
        return contentLength >= 0 && count - headerLength >= contentLength;
    }

    /// <summary>
    /// The body of a <c>200</c> answer, cut to <c>Content-Length</c> when the header is present; null for any
    /// other status, a malformed answer or a body shorter than announced.
    /// </summary>
    public static string? Body(byte[] data, int count)
    {
        var headerLength = FindHeaderEnd(data, count);
        if (headerLength < 12)
        {
            return null;
        }

        var statusLine = Encoding.ASCII.GetString(data, 0, 12);
        if (!statusLine.StartsWith("HTTP/1.", StringComparison.Ordinal) || statusLine.Substring(9, 3) != "200")
        {
            return null;
        }

        var bodyLength = count - headerLength;
        var contentLength = ContentLength(data, headerLength);
        if (contentLength >= 0)
        {
            if (bodyLength < contentLength)
            {
                return null;
            }

            bodyLength = contentLength;
        }

        return Encoding.UTF8.GetString(data, headerLength, bodyLength);
    }

    /// <summary>Length of the status line and headers including the blank line, or -1 if they are incomplete.</summary>
    private static int FindHeaderEnd(byte[] data, int count)
    {
        for (var i = 0; i + HeaderEnd.Length <= count; i++)
        {
            if (data[i] == HeaderEnd[0] && data[i + 1] == HeaderEnd[1] && data[i + 2] == HeaderEnd[2] && data[i + 3] == HeaderEnd[3])
            {
                return i + HeaderEnd.Length;
            }
        }

        return -1;
    }

    /// <summary>The <c>Content-Length</c> value, or -1 when it is absent or not a valid number.</summary>
    private static int ContentLength(byte[] data, int headerLength)
    {
        var headers = Encoding.ASCII.GetString(data, 0, headerLength).Split(new[] { "\r\n" }, StringSplitOptions.None);
        foreach (var line in headers)
        {
            var colon = line.IndexOf(':');
            if (colon <= 0 || !string.Equals(line.Substring(0, colon).Trim(), "Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return int.TryParse(line.Substring(colon + 1).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var length)
                ? length
                : -1;
        }

        return -1;
    }
}
