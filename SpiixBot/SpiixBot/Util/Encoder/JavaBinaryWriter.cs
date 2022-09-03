﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SpiixBot.Util.Encoder
{
    internal ref struct JavaBinaryWriter
    {
        private readonly Span<byte> _bytes;
        private int _currentWritePosition;

        public int Position
        {
            get => _currentWritePosition;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(Position),
                        "An attempt was made to move the position before the beginning.");
                }

                _currentWritePosition = Math.Min(value, Length);
            }
        }

        public int Length { get; private set; }

        public JavaBinaryWriter(Span<byte> bytes)
        {
            _bytes = bytes;
            _currentWritePosition = 0;
            Length = 0;
        }

        public void Write(string value)
        {
            var bytesToWrite = Encoding.UTF8.GetByteCount(value);

            Write((short)bytesToWrite);

            if (Position < Length)
            {
                _bytes.Slice(Position, Length - Position).CopyTo(_bytes.Slice(Position + bytesToWrite, Length - Position));
            }

            Encoding.UTF8.GetBytes(value, _bytes.Slice(Position, bytesToWrite));
            Length += bytesToWrite;
            Position += bytesToWrite;
        }

        public void Write<T>(T value) where T : struct
        {
            var bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1));

            if (BitConverter.IsLittleEndian)
            {
                bytes.Reverse();
            }

            Write(bytes);
        }

        public void Write(Span<byte> bytes)
        {
            if (Position < Length)
            {
                _bytes.Slice(Position, Length - Position).CopyTo(_bytes.Slice(Position + bytes.Length, Length - Position));
            }

            bytes.CopyTo(_bytes.Slice(Position, bytes.Length));
            Length += bytes.Length;
            Position += bytes.Length;
        }

        public int Seek(int offset, SeekOrigin origin)
        {
            return Position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => Position + offset,
                SeekOrigin.End => Length + offset,
                _ => Position
            };
        }

        public void WriteNullableText(string value)
        {
            var isValidString = !string.IsNullOrWhiteSpace(value);

            Write(isValidString);

            if (isValidString)
            {
                Write(value);
            }
        }

        public void WriteVersioned(int flags)
        {
            var value = Length | flags << 30;
            Seek(0, System.IO.SeekOrigin.Begin);
            Write(value);
            Seek(0, System.IO.SeekOrigin.End);
        }
    }
}
