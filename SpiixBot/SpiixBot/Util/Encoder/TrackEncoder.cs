using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Text;

namespace SpiixBot.Util.Encoder
{
    internal struct TrackEncoder
    {
        public static string Encode(Victoria.LavaTrack track)
        {
            return Encode(
                track.Title,
                track.Author,
                (long)track.Duration.TotalMilliseconds,
                track.Id,
                track.IsStream,
                track.Url
            );
        }

        public static string Encode(string title, string author, long durationMs, string id, bool isStream, string url, long position = 0)
        {
            JavaBinaryEncoder encoder = new JavaBinaryEncoder();

            encoder.WriteUTF8(title);
            encoder.WriteUTF8(author);
            encoder.WriteLong(durationMs);
            encoder.WriteUTF8(id);
            encoder.WriteBool(isStream);
            encoder.WriteBool(true);
            encoder.WriteUTF8(url);
            encoder.WriteUTF8("youtube");
            encoder.WriteLong(position);

            return Convert.ToBase64String(encoder.GetAsByteArray());

        }
    }
}
