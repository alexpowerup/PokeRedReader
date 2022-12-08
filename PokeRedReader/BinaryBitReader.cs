using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeRedReader
{
    public class BinaryBitReader : BinaryReader
    {
        public BinaryBitReader(Stream input) : base(input) { }

        public BinaryBitReader(Stream input, Encoding encoding) : base(input, encoding) { }

        public BinaryBitReader(Stream input, Encoding encoding, bool leaveOpen) : base(input, encoding, leaveOpen) { }

        private byte bitIndex = 0;
        private byte? currentByte = null;

        public byte ReadBit()
        {
            if (this.currentByte == null || this.bitIndex == 0) this.currentByte = this.ReadByte();

            byte ret = (byte)((this.currentByte >> (7 - this.bitIndex) & 0b1));

            this.bitIndex++;

            if (this.bitIndex > 7) this.bitIndex = 0;

            return ret;
        }
    }
}
