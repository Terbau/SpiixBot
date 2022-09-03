using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Victoria;

namespace SpiixBot.Util.Encoder
{
    internal struct TrackEncoder
    {
        private const int TRACK_VERSIONED = 1;
        private const int TRACK_VERSION = 2;

        private static LavaTrack CreateLavaTrack(string title, string author, long durationMs, string id, bool isStream, string url, long position = 0)
        {
            return new LavaTrack(
                hash: "",
                id: id,
                position: TimeSpan.FromMilliseconds(position),
                title: title,
                author: author,
                url: url,
                duration: durationMs,
                canSeek: true,
                isStream: isStream,
                source: "youtube"
                );
        }

        public static string Encode(string title, string author, long durationMs, string id, bool isStream, string url, long position = 0)
        {
            return Encode(CreateLavaTrack(title, author, durationMs, id, isStream, url, position));
        }

        public static string Encode(LavaTrack track)
        {
            Span<byte> bytes = stackalloc byte[GetByteCount(track)];

            var javaWriter = new JavaBinaryWriter(bytes);
            javaWriter.Write<byte>(TRACK_VERSION);
            javaWriter.Write("");  // title
            javaWriter.Write("");  // author
            javaWriter.Write((long)track.Duration.TotalMilliseconds);
            javaWriter.Write(track.Id);
            javaWriter.Write(track.IsStream);
            javaWriter.WriteNullableText(track.Url); // Extension method
            javaWriter.Write(track.Source);
            javaWriter.Write((long)track.Position.TotalMilliseconds);
            javaWriter.WriteVersioned(TRACK_VERSIONED); // Extension method

            return Convert.ToBase64String(bytes);
        }

        private static int GetByteCount(LavaTrack track)
        {
            return 23 + typeof(LavaTrack).GetProperties()
                .Where(p => p.PropertyType == typeof(string) && p.Name != "Hash")
                .Sum(p => 2 + Encoding.UTF8.GetByteCount(p.GetValue(track).ToString()));
        }
    }
}
