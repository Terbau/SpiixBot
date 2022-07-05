using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace SpiixBot.Util.Encoder
{
    internal class JavaBinaryEncoder
    {
        private List<byte> _bytes = new List<byte>();

        public void WriteUTF8(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            short length = (short)bytes.Length;

            WriteShort(length);
            Write(bytes, reverse: false);
        }

        public void WriteShort(short value)
        {
            var bytes = BitConverter.GetBytes(value);
            Write(bytes);
        }

        public void WriteInt32(int value)
        {
            var bytes = BitConverter.GetBytes(value);
            Write(bytes);
        }

        public void WriteLong(long value)
        {
            var bytes = BitConverter.GetBytes(value);
            Write(bytes);
        }

        public void WriteBool(bool value)
        {
            var bytes = BitConverter.GetBytes(value);
            Write(bytes);
        }

        public void WriteSbyte(sbyte value)
        {
            Write(new byte[] { (byte)value });
        }

        public void Write(byte[] bytes, bool reverse = true)
        {
            if (reverse) Array.Reverse(bytes);

            foreach (byte byte_ in bytes)
            {
                _bytes.Add(byte_);
            }
        }

        public byte[] GetAsByteArray()
        {
            int headerValue = 0x40000000 ^ (_bytes.Count + 1);

            byte version = 2;
            byte[] header = BitConverter.GetBytes(headerValue);
            Array.Reverse(header);

            _bytes.Insert(0, version);
            _bytes.InsertRange(0, header);

            return _bytes.ToArray();
        }
    }
}
