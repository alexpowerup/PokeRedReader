using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeRedReader
{
    public class BinaryBitWriter : BinaryWriter
    {
        public BinaryBitWriter(Stream input) : base(input) { }

        public BinaryBitWriter(Stream input, Encoding encoding) : base(input, encoding) { }

        public BinaryBitWriter(Stream input, Encoding encoding, bool leaveOpen) : base(input, encoding, leaveOpen) { }

        private byte bitIndex = 0;

        public void WriteBit(byte bit)
        {
            //Force bit to be either 1 or 0
            bit = (byte)((bit > 0) ? 1 : 0);

            //Get current memory location data and don't advance the stream position
            byte mem = (byte)(this.BaseStream.ReadByte());
            this.BaseStream.Position--;

            //Make the change
            byte mask = (byte)(1 << (7 - this.bitIndex));
            byte ret = (byte)((mem & ~mask) | ((bit << (7 - this.bitIndex)) & mask));

            //Console.WriteLine("WRITING {0}: BIT {1} - OG {2} - FINAL {3}", bit, this.bitIndex, Convert.ToString(mem, 2), Convert.ToString(ret, 2));

            //Commit it
            this.Write(ret);

            //Advance the bit index
            this.bitIndex++;

            //If the byte is done, reset the bit index and leave position advanced
            if (this.bitIndex > 7) this.bitIndex = 0;

            //If not, set position on last byte fetched
            else this.BaseStream.Position--;
        }

        UInt16
            Bite = 0,
            Start = 0,
            Width = 0,
            Height = 0,
            X = 0,
            Y = 0,
            BitCount = 0
        ;
        byte Bit = 0;


        public void SetSize(UInt16 start, UInt16 width, UInt16 height, bool flipped = false)
        {
            this.Start = start;
            this.Width = width;
            this.Height = height;
            this.Bite = start;
            this.Bit = flipped ? (byte)6 : (byte)0;
            this.X = flipped ? (UInt16)(width - 2) : (UInt16)0;
            this.Y = 0;
            this.BitCount = 0;
        }

        public bool HasNext()
        {
            return (this.X < this.Width && this.X >= 0);
        }

        public byte PeekByte()
        {
            var p = this.BaseStream.Position;
            byte ret = (byte)this.BaseStream.ReadByte();
            this.BaseStream.Position = p;
            return ret;
        }

        public void WriteByteStealth(byte a)
        {
            var p = this.BaseStream.Position;
            this.BaseStream.WriteByte(a);
            this.BaseStream.Position = p;
        }

        public bool WritePair(byte a, byte b)
        {
            //Ensure only the first bit is used on parameters
            a &= 1;
            b &= 1;

            if (this.Bite < this.BaseStream.Length)
            {
                this.BaseStream.Position = this.Bite;
                var n = this.PeekByte();
                this.WriteByteStealth((byte)(n | (a << (7 - this.Bit) | (b << (6 - this.Bit)))));
                n = this.PeekByte();
            }

            this.Y++;

            if (this.Y >= this.Height)
            {
                this.Y = 0;
                this.X += 2;
            }

            this.Bit = (byte)(this.X % 8);
            this.Bite = (UInt16)(this.Start + Math.Floor((decimal)this.X / 8) * this.Height + this.Y);
            this.BitCount += 2;
            return this.HasNext();
        }
    }
}
