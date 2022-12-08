using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeRedReader
{
    public class SpriteDecompressor
    {
        /// <summary>
        /// ROMAccessor object from where the data will be collected.
        /// </summary>
        private ROMAccessor ROM;

        /// <summary>
        /// Maximum width of a sprite, in tiles (a tile is 8x8 pixels).
        /// </summary>
        public readonly byte MaxWidth;

        /// <summary>
        /// Maximum height of a sprite, in tiles (a tile is 8x8 pixels).
        /// </summary>
        public readonly byte MaxHeight;

        /// <summary>
        /// The game uses three buffers for its sprite decompressing algorithm.
        /// </summary>
        private byte[] Buffers;

        /// <summary>
        /// Bit reader for the ROM.
        /// </summary>
        private BinaryBitReader reader;

        /// <summary>
        /// Writer for the buffers.
        /// </summary>
        private BinaryBitWriter writer;

        /// <summary>
        /// Offset table for RLE.
        /// </summary>
        private UInt16[] RLEOffsets;

        public SpriteDecompressor(ROMAccessor ROM, byte MaxWidth= 0x7, byte MaxHeight = 0x7)
        {
            this.ROM = ROM;
            this.MaxWidth = MaxWidth;
            this.MaxHeight = MaxHeight;
            //Buffers must be much longer than specified dimensions because overflows can happen.
            this.Buffers = new byte[0xFFFF];

            this.reader = new BinaryBitReader(this.ROM.MemoryReader.BaseStream);
            this.writer = new BinaryBitWriter(new MemoryStream(this.Buffers));

            this.RLEOffsets = this.ROM.GetRLEOffsetTable();
        }

        /// <summary>
        /// Obtains the bank that should be loaded to get sprites from a certain pokémon.
        /// </summary>
        /// <param name="id">Pokémon's ID.</param>
        /// <returns></returns>
        public byte GetMonSpriteBank(byte id)
        {
            //Mew is at bank 0x01
            if (id == 0x15) return 0x01;

            //Kabutops Fossil is at bank 0x0B
            if (id == 0xB6) return 0x0B;

            //By range
            if (id < 0x1F) return 0x09;
            if (id < 0x4A) return 0x0A;
            if (id < 0x74) return 0x0B;
            if (id < 0x99) return 0x0C;
            return 0x0D;
        }

        public byte[] DecompressFront(byte id)
        {
            //Some exceptions are hardcoded into the game
            if (id == 0xB6)
                return this.Decompress(0x0B, 0x79E8, 6, 6);

            if (id == 0xB7)
                return this.Decompress(0x0D, 0x6536, 7, 7);

            if (id == 0xB8)
                return this.Decompress(0x0D, 0x66B5, 6, 6);

            var baseStats = this.ROM.GetMonBaseStats(this.ROM.GetDexFromId(id));

            return this.Decompress(GetMonSpriteBank(id), baseStats.FrontSpritePointer, baseStats.SpriteDimensionX, baseStats.SpriteDimensionY);
        }

        public byte[] DecompressBack(byte id)
        { 
            var baseStats = this.ROM.GetMonBaseStats(this.ROM.GetDexFromId(id));

            return this.Decompress(GetMonSpriteBank(id), baseStats.BackSpritePointer, 4, 4);
        }

        public byte[] Decompress(byte bank, UInt16 address, byte? widthStats_ = null, byte? heightStats_ = null)
        {
            /*
            How compressed sprites are stored:
            -Nibble - Width (tiles)
            -Nibble - Height (tiles)
            -Bit    - Primary buffer position (0 -> B; 1 -> C)
            -Bit    - Initial packet (0 -> RLE; 1 -> DATA)
            -Stream - Primary bitplane
            -Bit    - Encoding mode (0 -> MODE 1; 1 -> ANOTHER)
            -(if Encoding Method bit is 1)
                -Bit- Another encoding mode (0 -> MODE 2; 1 -> MODE 3)
            -Bit    - Initial packet (0 -> RLE; 1 -> DATA)
            -Stream - Secondary bitplane

            How enconding modes are executed:
            -ANY MODE - Delta decode primary bitplane. (This is done first)
            -MODE 1   - Delta decode secondary bitplane.
            -MODE 2   - XOR both bitplanes and place result at secondary bitplane.
            -MODE 3   - Delta decode secondary bitplane. Then, XOR both bitplanes and place result at secondary bitplane.
            */

            //RESET BUFFERS
            this.Buffers = new byte[0xFFFF];

            //RESET WRITERS AND READERS
            this.reader = new BinaryBitReader(this.ROM.MemoryReader.BaseStream);
            this.writer = new BinaryBitWriter(new MemoryStream(this.Buffers));

            //STEP 1 - Load selected bank and jump to corresponding address
            this.ROM.LoadBankN(bank);
            this.ROM.MemoryReader.BaseStream.Position = address;

            //STEP 2 - Read data from the sprite and set up buffer pointers
            UInt16 bufferSize = (UInt16)(this.MaxWidth * this.MaxHeight * 8);
            UInt16
                bufferAPtr = (UInt16)(bufferSize * 0),
                bufferBPtr = (UInt16)(bufferSize * 1),
                bufferCPtr = (UInt16)(bufferSize * 2)
            ;

            var width = this.ROM.MemoryReader.ReadNibble(NibbleLevel.LOW);
            var height = this.ROM.MemoryReader.ReadNibble(NibbleLevel.HIGH);

            //Set sprite size if not specified
            byte widthStats = widthStats_ == null ? width : widthStats_.Value;
            byte heightStats= heightStats_ == null ? height : heightStats_.Value;

            var primaryBufferPosition = this.reader.ReadBit();

            UInt16 primaryBitplaneBufferPtr = (primaryBufferPosition == 0) ? bufferBPtr : bufferCPtr;
            UInt16 secondaryBitplaneBufferPtr = (primaryBufferPosition == 1) ? bufferBPtr : bufferCPtr;

            this.GetBitplane(primaryBitplaneBufferPtr, width, height);

            byte mode;
            if(this.reader.ReadBit() == 0)
            {
                mode = 1;
            }
            else
            {
                if(this.reader.ReadBit() == 0)
                    mode = 2;
                else
                    mode = 3;
            }

            this.GetBitplane(secondaryBitplaneBufferPtr, width, height);

            //STEP 3 - Process encoding mode
            switch(mode)
            {
                case 1: //MODE 1 - Delta decode primary bitplane
                    this.DeltaDecode(secondaryBitplaneBufferPtr, width, height);
                    this.DeltaDecode(primaryBitplaneBufferPtr, width, height);
                    break;
                case 2: //MODE 2 - XOR both bitplanes and place result at secondary
                    this.DeltaDecode(primaryBitplaneBufferPtr, width, height);
                    this.XOR(primaryBitplaneBufferPtr, secondaryBitplaneBufferPtr, secondaryBitplaneBufferPtr, width, height);
                    break;
                case 3: //MODE 3- Do both things
                    this.DeltaDecode(secondaryBitplaneBufferPtr, width, height);
                    this.DeltaDecode(primaryBitplaneBufferPtr, width, height);
                    this.XOR(primaryBitplaneBufferPtr, secondaryBitplaneBufferPtr, secondaryBitplaneBufferPtr, width, height);
                    break;
            }

            //STEP 4 - Copy bitplane on buffer B to buffer A centered, then do the same from buffer C to buffer B.
            this.CopySpriteCentered(bufferBPtr, bufferAPtr, widthStats, heightStats);
            this.CopySpriteCentered(bufferCPtr, bufferBPtr, widthStats, heightStats);

            //STEP 5 - Merge both bitplanes
            this.MergeBitplanes();

            //STEP 6 - Sequentially copy buffers A and B. That data is the final sprite.
            byte[] ret = new byte[bufferSize * 2];
            Array.Copy(this.Buffers, 0, ret, 0, bufferSize * 2);

            return ret;
        }

        /// <summary>
        /// Reads bitplane from ROM and decompresses RLE and DATA parts.
        /// </summary>
        /// <param name="position">Buffer's memory location to store the decompressed bitplane.</param>
        /// <param name="width">Width of the sprite.</param>
        /// <param name="height">Height of the sprite.</param>
        public void GetBitplane(UInt16 position, byte width, byte height)
        {
            this.writer.BaseStream.Position = position;
            this.writer.SetSize(position, (UInt16)((int)width * 8), (UInt16)((int)height * 8));

            var bytesToWrite = width * height * 8;
            var bitsToWrite = bytesToWrite * 8;

            var mode = this.reader.ReadBit();
            int bitsWritten = 0;
            do
            {
                bool[] fetched = null;
                if(mode == 0) //RLE
                {
                    fetched = this.ReadRLEPacket();
                }
                else //DATA
                {
                    fetched = this.ReadDATAPacket();
                }

                //Write fetched data into the buffer in pairs
                /*
                for (int i = 0; i < fetched.Length; i += 2)
                {
                    if(!this.writer.WritePair((byte)(fetched[i] ? 1 : 0), (byte)(fetched[i+1] ? 1 : 0)))
                    {
                        break;
                    }
                }
                */
                bitsWritten += fetched.Length;

                //when a packet ends, compression mode toggles
                mode ^= 1;
            } while (bitsWritten < bitsToWrite);

            return;
        }

        /// <summary>
        /// Decompresses an RLE packet.
        /// </summary>
        /// <returns></returns>
        public bool[] ReadRLEPacket()
        {
            /*
            An RLE packet has variable length. It works as follows:
            -Read bits and store them somwehere as an unsigned integer until a 0 is found.
            -Read that many bits again and store them somewhere else as an unsigned integer.
            -The result will be a stream of a pair of 0 bits (00), which length is the result
             of the first number, plus the second number, plus one.

            Example: 1111001110
            -First = 11110 -> 30
            -Second = 01110 -> 14
            -Result = First + Second + 1 = 30 + 14 + 1 = 45 00 bits in a row
            */
            UInt16 first = 0;
            int count = 0;
            bool fetched = true;
            do
            {
                fetched = this.reader.ReadBit() == 1;
                first = (UInt16)((first << 1) + (fetched ? 1 : 0));
                count++;
            } while (fetched);

            UInt16 second = 0;
            for(UInt16 i = 0; i < count; i++)
            {
                fetched = this.reader.ReadBit() == 1;
                second = (UInt16)((second << 1) + (fetched ? 1 : 0));
            }

            UInt16 totalPairs = (UInt16)(first + second + 1);

            for(UInt16 i = 0; i < totalPairs; i++)
            {
                if (!this.writer.HasNext()) break;
                this.writer.WritePair(0, 0);
            }

            return Enumerable.Repeat<bool>(false, totalPairs * 2).ToArray();
        }

        /// <summary>
        /// "Decompresses" a DATA packet.
        /// </summary>
        /// <returns></returns>
        public bool[] ReadDATAPacket()
        {
            /*
            A DATA packet has variable length. It works as follows:
            -Read two bits. If both bits are 0 (00), the process stops.
            -If the process has not stopped, those two bits are stored to be sent when
             the process ends. There is no compression whatsoever, hence the packet name.
            */
            var ret = new List<bool>();
            var fetched = new bool[2];
            int countPair = 0;
            do
            {
                fetched[0] = this.reader.ReadBit() > 0;
                fetched[1] = this.reader.ReadBit() > 0;

                if(fetched[0] || fetched[1])
                {
                    ret.Add(fetched[0]);
                    ret.Add(fetched[1]);
                    this.writer.WritePair(fetched[0] ? (byte)1 : (byte)0, fetched[1] ? (byte)1 : (byte)0);
                    countPair++;
                }
            } while (this.writer.HasNext() && (fetched[0] || fetched[1]));

            return ret.ToArray();
        }

        /// <summary>
        /// Delta decodes a buffer.
        /// </summary>
        /// <param name="start">The start of the delta decoding process.</param>
        /// <param name="width">Width of the sprite in tiles.</param>
        /// <param name="height">Height of the sprite in tiles.</param>
        public void DeltaDecode(UInt16 start, byte width, byte height)
        {
            byte state;
            int widthPixels = width * 8;
            int heightPixels = height* 8;

            for (UInt16 y = 0; y < heightPixels; y++)
            {
                state = 0;
                for (UInt16 nx = 0; nx < (UInt16)Math.Floor((decimal)widthPixels/4); nx++)
                {
                    byte nibble = 0;
                    UInt16 i = (UInt16)(start + (UInt16)Math.Floor((decimal)nx / 2) * (UInt16)heightPixels + y);
                    for (UInt16 px = 0; px < 4; px++)
                    {
                        var p = 4 * (nx % 2) + px;
                        var bit = 1 & (this.Buffers[i] >> (7-p));
                        if (bit == 1) state ^= 1;
                        nibble = (byte)(state | (nibble << 1));
                    }

                    for (UInt16 px = 0; px < 4; px++)
                    {
                        var p = 4 * (nx % 2) + px;
                        var bit = 1 & (nibble >> (3 - px));
                        this.Buffers[i] = (byte)(0xFF & ((this.Buffers[i] & ~(1 << (7 - p))) | (bit << (7  - p))));
                    }
                }
            }
        }

        /// <summary>
        /// XORs two buffers and store the result at the selected destination.
        /// </summary>
        /// <param name="buffer1Ptr">Start of the first buffer to be XOR'd.</param>
        /// <param name="buffer2Ptr">Start of the second buffer to be XOR'd.</param>
        /// <param name="bufferDstPtr">Start of the location the result will be stored at.</param>
        /// <param name="width">Width of the sprite in tiles.</param>
        /// <param name="height">Height of the sprite in tiles.</param>
        public void XOR(UInt16 buffer1Ptr, UInt16 buffer2Ptr, UInt16 bufferDstPtr, byte width, byte height)
        {
            UInt16 bufferSize = (UInt16)(width * height * 8);
            for(UInt16 i = 0; i < bufferSize; i++)
            {
                this.Buffers[bufferDstPtr + i] = (byte)(this.Buffers[buffer1Ptr + i] ^ this.Buffers[buffer2Ptr + i]);
            }
        }

        /// <summary>
        /// Copies the sprite from the src buffer to the dst buffer and centers it.
        /// </summary>
        /// <param name="src">Location of the sprite in the buffers.</param>
        /// <param name="dst">Location of the destination of the result in the buffers.</param>
        /// <param name="width">Width of the sprite in tiles.</param>
        /// <param name="height">Height of the sprite in tiles.</param>
        public void CopySpriteCentered(UInt16 src, UInt16 dst, byte width, byte height)
        {
            //Fill destination buffer with 0x00 bytes to clear it
            for (int i = 0; i < (this.MaxWidth * this.MaxHeight * 8); i++)
            {
                this.Buffers[dst + i] = 0x00;
            }

            //Calculate horizontal and vertical offsets
            byte horizontalOffset = (byte)((int)(Math.Floor((decimal)(this.MaxWidth + 1 - width) / 2)) & 0xFF);
            byte verticalOffset = (byte)(this.MaxHeight - height);

            UInt16 readPtr = src;
            byte offset_ = (byte)(8 * (this.MaxHeight * horizontalOffset + verticalOffset));
            UInt16 offset = (UInt16)(offset_ + dst);

            UInt16 w = width;
            if (w == 0) w = 0x100;

            UInt16 h = height;
            if (h == 0) h = 0x20;

            for (UInt16 x = 0; x < w; x++)
            {
                UInt16 writePtr = offset;
                for (UInt16 y = 0; y < h; y++)
                {
                    for (byte p = 0; p < 8; p++)
                    {
                        if (writePtr < this.Buffers.Length && readPtr < this.Buffers.Length)
                        {
                            this.Buffers[writePtr] = this.Buffers[readPtr];
                        }
                        writePtr++;
                        readPtr++;
                    }
                }
                offset += (UInt16)(8 * this.MaxHeight);
            }
        }

        /// <summary>
        /// Merges both bitplanes into the final 2bpp one.
        /// </summary>
        public void MergeBitplanes()
        {
            /*
            At this point, buffers A and B should be filled with both bitplanes.
            The way merging works is simply interlacing both bitplanes, reading
            from bottom to top and writing the result from the end of buffer C.
            */
            int bufferSize = (int)this.MaxWidth * (int)this.MaxHeight * 8;

            for(int i = bufferSize - 1; i >= 0; i--)
            {
                byte plane1 = this.Buffers[bufferSize + i];
                byte plane0 = this.Buffers[i];

                this.Buffers[bufferSize + (2 * i) + 1] = plane1;
                this.Buffers[bufferSize + (2 * i)] = plane0;
            }
        }

        public Image RenderSprite(Color[] palette = null)
        {
            var ret = new Bitmap(this.MaxWidth * 8, this.MaxHeight * 8);
            int bytesPerPlane = 8 * this.MaxWidth * this.MaxHeight;

            if(palette == null || palette.Length < 4)
            {
                palette = new Color[4];
                palette[0] = Color.FromArgb(0xFF, 0xFF, 0xFF);
                palette[1] = Color.FromArgb(0xBB, 0xBB, 0xBB);
                palette[2] = Color.FromArgb(0x77, 0x77, 0x77);
                palette[3] = Color.FromArgb(0x00, 0x00, 0x00);
            }

            for (int x = 0; x < this.MaxWidth; x++)
            {
                for (int y = 0; y < this.MaxHeight; y++)
                {
                    for (int py = 0; py < 8; py++)
                    {
                        for(int px = 0; px < 8; px++)
                        {
                            int i = 0;
                            int bite = (this.MaxHeight * 8 * 2 * x) + (8 * 2 * y) + (2 * py);
                            if(bite < this.Buffers.Length)
                            {
                                int b0 = (this.Buffers[bytesPerPlane + bite + 0] >> (7 - px)) & 1;
                                int b1 = (this.Buffers[bytesPerPlane + bite + 1] >> (7 - px)) & 1;
                                i = (b1 << 1) | b0;
                            }

                            ret.SetPixel(8 * x + px, 8 * y + py, palette[i]);
                        }
                    }
                }
            }

            return ret;
        }
    }
}
