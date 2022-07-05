using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Text;

namespace SpiixBot.Util.Decoder
{
    public readonly struct TrackDecoder2
    {
        public static void Decode(string trackHash)
        {
            Span<byte> hashBuffer = stackalloc byte[trackHash.Length];
            Encoding.ASCII.GetBytes(trackHash, hashBuffer);
            Base64.DecodeFromUtf8InPlace(hashBuffer, out var bytesWritten);
            var javaReader = new JavaBinaryReader2(hashBuffer[..bytesWritten]);

            // Reading header
            var header = javaReader.Read<int>();
            var flags = (int)((header & 0xC0000000L) >> 30);
            var hasVersion = (flags & 1) != 0;
            var _ = hasVersion
                ? javaReader.Read<sbyte>()
                : 1;

            //Console.WriteLine((header & 0xC0000000L).ToString("X"));
            //Console.WriteLine(((header & 0xC0000000L) >> 30).ToString("X"));
            Console.WriteLine(header.ToString("X"));
            Console.WriteLine(flags);
            Console.WriteLine(hasVersion);
            Console.WriteLine(_.ToString("X"));

            Console.WriteLine("Title");
            javaReader.ReadString();
            Console.WriteLine("Title");
            javaReader.ReadString();
            Console.WriteLine("Title");
            javaReader.Read<long>();
            Console.WriteLine("Title");
            javaReader.ReadString();
            Console.WriteLine("Title");
            javaReader.Read<bool>();
            Console.WriteLine("Title");
            javaReader.Read<bool>();
            Console.WriteLine("Title");
            javaReader.ReadString();

            //var track = new Victoria.LavaTrack(
            //    trackHash,
            //    title: javaReader.ReadString(),
            //    author: javaReader.ReadString(),
            //    duration: javaReader.Read<long>(),
            //    id: javaReader.ReadString(),
            //    isStream: javaReader.Read<bool>(),
            //    url: javaReader.Read<bool>()
            //        ? javaReader.ReadString()
            //        : string.Empty,
            //    position: default,
            //    canSeek: true);

            //return track;
        }
    }
}
