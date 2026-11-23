using System.Text;

namespace HashForge.Stratum.Transport;

/// <summary>Reads newline-terminated text from a stream, refusing lines longer than a fixed limit.</summary>
public sealed class LineReader(Stream stream, int maxLineLength)
{
    private const byte LineFeed = (byte)'\n';

    private readonly byte[] _buffer = new byte[maxLineLength];
    private int _start;
    private int _end;

    public async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var newline = Array.IndexOf(_buffer, LineFeed, _start, _end - _start);
            if (newline >= 0)
            {
                var line = Encoding.UTF8.GetString(_buffer, _start, newline - _start).TrimEnd('\r');
                _start = newline + 1;
                return line;
            }

            Compact();
            if (_end == _buffer.Length)
            {
                throw new InvalidDataException($"Line exceeds the {maxLineLength} byte limit.");
            }

            var read = await stream.ReadAsync(_buffer.AsMemory(_end), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            _end += read;
        }
    }

    /// <summary>Moves unread bytes to the front of the buffer to make room for more.</summary>
    private void Compact()
    {
        if (_start == 0)
        {
            return;
        }

        Buffer.BlockCopy(_buffer, _start, _buffer, 0, _end - _start);
        _end -= _start;
        _start = 0;
    }
}
