using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeRedReader
{
    public class PokeRedStreamReader : BinaryReader
    {
        /// <summary>
        /// Byte that marks the end of a string.
        /// </summary>
        static public readonly byte StringTerminator = 0x50;

        /// <summary>
        /// Byte that marks the start of a text jump. Next UInt16 is the address and next Byte is the bank.
        /// </summary>
        static public readonly byte TX_FAR = 0x17;

        /// <summary>
        /// Translation table for texts in the game. Loaded just when needed and stays on memory.
        /// </summary>
        private Dictionary<byte, string> TextTranslationTable = null;

        /// <summary>
        /// Keeps track of what bit should be read next.
        /// </summary>
        private byte BitReadPosition = 0;

        /// <summary>
        /// Stores the last byte read by the ReadBit method.
        /// </summary>
        private byte? LastByteReadByBits = null;

        /// <summary>
        /// ROM Accessor object that owns this reader.
        /// </summary>
        private ROMAccessor ROM;

        public PokeRedStreamReader(Stream input, ROMAccessor ROM): base(input)
        {
            this.ROM = ROM;
        }

        public PokeRedStreamReader(Stream input, Encoding encoding, ROMAccessor ROM): base(input, encoding)
        {
            this.ROM = ROM;
        }

        public PokeRedStreamReader(Stream input, Encoding encoding, bool leaveOpen, ROMAccessor ROM) : base(input, encoding, leaveOpen)
        {
            this.ROM = ROM;
        }

        /// <summary>
        /// Gets the dictionary that converts game text to readable text.
        /// </summary>
        /// <returns></returns>
        public Dictionary<byte, string> GetTextTranslationTable()
        {
            //Return the loaded version if available
            if (this.TextTranslationTable != null) return this.TextTranslationTable;

            //Load dictionary from JSON
            var json = File.ReadAllText("TextTranslationTable.json");
            var tmpTransTable = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

            this.TextTranslationTable = new Dictionary<byte, string>();

            //Requires conversion for hexadecimal values
            foreach (KeyValuePair<string, string> entry in tmpTransTable)
            {
                this.TextTranslationTable.Add(Convert.ToByte(entry.Key, 16), entry.Value);
            }

            return this.TextTranslationTable;
        }

        /// <summary>
        /// Overrides original function to make it loop, just as what the console actually would do.
        /// </summary>
        /// <returns></returns>
        public override byte ReadByte()
        {
            this.ResetBitReading();

            if (this.BaseStream.Position >= this.BaseStream.Length) this.BaseStream.Position = 0;

            return base.ReadByte();
        }

        /// <summary>
        /// Overrides original function to make it loop, just as what the console actually would do.
        /// </summary>
        /// <returns></returns>
        public override UInt16 ReadUInt16()
        {
            this.ResetBitReading();

            if (this.BaseStream.Position >= this.BaseStream.Length) this.BaseStream.Position = 0;

            //Variable divided between end and start of stream
            if (this.BaseStream.Position == this.BaseStream.Length - 1)
            {
                var part2 = this.ReadByte();
                this.BaseStream.Position = 0;
                var part1 = this.ReadByte();

                return (UInt16)((part1 << 8) & part2);
            }

            return base.ReadUInt16();
        }

        /// <summary>
        /// Reads a nibble from current position.
        /// </summary>
        /// <param name="level">Read HIGH nibble or LOW nibble. If LOW is read, position will not advance.</param>
        /// <returns></returns>
        public byte ReadNibble(NibbleLevel level)
        {
            //read byte
            var data = this.ReadByte();

            if (level == NibbleLevel.LOW) //Read low nibble, don't advance cursor
            {
                this.BaseStream.Position--;
                return (byte)(0b00001111 & data);
            }
            else //Read high nibble, let cursor advanced
            {
                return (byte)((0b11110000 & data) >> 4);
            }
        }

        /// <summary>
        /// Gets a game-formatted string.
        /// </summary>
        /// <param name="length">Bytes to be read. If null, it will stop reading when a string terminator is found.</param>
        /// <param name="showTerminator">If true, terminators found will be written as "@".</param>
        /// <param name="ignoreTXFAR">If true, TX_FAR instructions will not be executed.</param>
        /// <returns></returns>
        public string ReadGameString(UInt16? length = null, bool showTerminator = false, bool ignoreTXFAR = false)
        {
            var table = this.GetTextTranslationTable();

            string ret = "";

            byte character;

            UInt16? addressToRestore = null;
            byte? bankToRestore = null;

            UInt16 count = 0;
            while ((length == null) || (count < length))
            {
                character = this.ReadByte();

                //If the character is TX_FAR, the text continues from another location and bank
                if(!ignoreTXFAR && character == PokeRedStreamReader.TX_FAR)
                {
                    var positionToJump = this.ReadUInt16();
                    var bankToJump = this.ReadByte();

                    this.ReadByte(); //Should be a terminator

                    addressToRestore = (UInt16)(this.BaseStream.Position);
                    bankToRestore = this.ROM.SecondBankLoaded;

                    //Make the jump
                    this.ROM.LoadBankN(bankToJump);
                    this.BaseStream.Position = positionToJump;
                    character = this.ReadByte();
                }

                count++;

                //If a character is not in the translation table, show a question mark
                if (!table.ContainsKey(character))
                {
                    ret += "<?>"; continue;
                }

                if (character == PokeRedStreamReader.StringTerminator)
                {
                    if (showTerminator) ret += table[character];

                    //End the process if a terminator is found and length is not specified
                    if (length == null) break;
                }
                else
                {
                    ret += table[character];
                }
            }

            //Restore bank and position if they were altered
            if(addressToRestore != null && bankToRestore != null)
            {
                this.ROM.LoadBankN((byte)bankToRestore);
                this.BaseStream.Position = (long)addressToRestore;
            }

            return ret;
        }

        /// <summary>
        /// Gets the flags starting from current position. Flags are converted to bool.
        /// </summary>
        /// <param name="length">Number of flags to be read.</param>
        /// <returns></returns>
        public bool[] ReadFlags(byte length)
        {
            bool[] ret = new bool[length];

            byte currentByte = 0x00;

            for(byte count = 0; count < length; count++)
            {
                byte bitToRead = (byte)(count % 8);
                byte mask = (byte)(0x1 << bitToRead);

                if (bitToRead == 0) currentByte = this.ReadByte();

                ret[count] = (((currentByte & mask) >> bitToRead) == 1);
            }

            return ret;
        }

        /// <summary>
        /// Gets a table with stream readers, each with the determined length.
        /// </summary>
        /// <param name="itemLength">Length of each table item.</param>
        /// <param name="entries">Number of entries to be fetched.</param>
        /// <returns></returns>
        public PokeRedStreamReader[] ReadTable(UInt16 itemLength, int entries = 0xFF)
        {
            var ret = new PokeRedStreamReader[entries+1];

            for (int i = 0; i <= entries; i++)
            {
                var tmp = new byte[itemLength];
                for(UInt16 j = 0; j < itemLength; j++)
                {
                    tmp[j] = this.ReadByte();
                }
                ret[i] = new PokeRedStreamReader(new MemoryStream(tmp), this.ROM);
            }

            return ret;
        }

        /// <summary>
        /// Reads a pointer table, that is, each entry is a 16 bit number.
        /// </summary>
        /// <param name="entries">Number of entries to be fetched.</param>
        /// <returns></returns>
        public UInt16[] ReadPointerTable(int entries = 0xFF)
        {
            var rawTable = this.ReadTable(0x02, entries);

            var ret = new UInt16[entries+1];

            for(int i = 0; i <= entries; i++)
            {
                ret[i] = rawTable[i].ReadUInt16();
            }

            return ret;
        }

        /// <summary>
        /// Reads a game-formatted text table.
        /// </summary>
        /// <param name="itemLength">Length of each entry.</param>
        /// <param name="showTerminator">If true, the string terminator(s) will be displayed as an "@" symbol.</param>
        /// <param name="entries">Number of entries to be fetched.</param>
        /// <returns></returns>
        public string[] ReadGameStringTable(UInt16 itemLength, bool showTerminator = false, int entries = 0xFF)
        {
            var rawTable = this.ReadTable(itemLength, entries);

            var ret = new string[entries+1];

            for(int i = 0; i <= entries; i++)
            {
                ret[i] = rawTable[i].ReadGameString(itemLength, showTerminator);
            }

            return ret;
        }

        /// <summary>
        /// Reads a game-formatted text table that comes from a pointer table.
        /// </summary>
        /// <param name="showTerminator">If true, the string terminator(s) will be displayed as an "@" symbol.</param>
        /// <param name="entries">Number of entries to be fetched.</param>
        /// <returns></returns>
        public string[] ReadGameStringPointerTable(bool showTerminator = false, int entries = 0xFF)
        {
            var pointerTable = this.ReadPointerTable(entries);

            var ret = new string[entries+1];

            for(int i = 0; i <= entries; i++)
            {
                this.BaseStream.Position = pointerTable[i];
                ret[i] = this.ReadGameString(null, showTerminator);
            }

            return ret;
        }

        /// <summary>
        /// Reads a bit from current memory location.
        /// </summary>
        /// <returns></returns>
        public byte ReadBit()
        {
            if (this.BitReadPosition == 0 || this.LastByteReadByBits == null) this.LastByteReadByBits = this.ReadByte();
            var mask = 0x1 << this.BitReadPosition;
            byte ret = (byte)((this.LastByteReadByBits & mask) >> this.BitReadPosition);

            this.BitReadPosition++;
            if (this.BitReadPosition >= 8) this.BitReadPosition = 0;

            return (ret > 0) ? (byte)1 : (byte)0;
        }

        /// <summary>
        /// Resets state of bit reader.
        /// </summary>
        public void ResetBitReading()
        {
            this.BitReadPosition = 0;
            this.LastByteReadByBits = null;
        }

    }

    /// <summary>
    /// Manages what nibble of a byte is being fetched.
    /// </summary>
    public enum NibbleLevel
    {
        LOW = 0,
        HIGH = 1
    }
}
