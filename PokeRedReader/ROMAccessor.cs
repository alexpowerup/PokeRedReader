using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeRedReader
{
    /// <summary>
    /// This class handles every data recollecting from the ROM.
    /// </summary>
    public class ROMAccessor
    {
        //CONSTANTS

        /// <summary>
        /// The size of a ROM bank. GameBoy reads ROM from 0x0000 to 0x7FFF, for a total of 0x8000 bytes.
        /// When a cartridge has banks, the first 0x4000 bytes always references bank 0, and the rest
        /// references the bank currently loaded. Thus, a bank has a size of 0x4000 bytes.
        /// </summary>
        static readonly UInt16 bankSize = 0x4000;

        /// <summary>
        /// "Memory" size of the system. GameBoy can redirect up to 16 bits, so the size is 0xFFFF, or 64K.
        /// </summary>
        static readonly UInt16 memorySize = 0xFFFF;

        /// <summary>
        /// Where the name of the game starts in memory.
        /// </summary>
        static readonly UInt16 nameAddress = 0x0134;

        /// <summary>
        /// Size of the name of the game.
        /// </summary>
        static readonly byte nameSize = 0xF;

        //INNER STUFF

        /// <summary>
        /// Loaded ROM.
        /// </summary>
        private byte[] ROM = null;

        /// <summary>
        /// Current memory of the game. The ROM can't be read directly because instructions are made to work
        /// based on system memory. Also, out of bounds data is fetched from this memory as well.
        /// </summary>
        private byte[] Memory = null;

        /// <summary>
        /// Custom streamreader that accesses to memory.
        /// </summary>
        public PokeRedStreamReader MemoryReader = null;

        /// <summary>
        /// Current bank loaded in slot 0.
        /// </summary>
        public byte FirstBankLoaded;

        /// <summary>
        /// Current bank loaded in slot 1.
        /// </summary>
        public byte SecondBankLoaded;

        public ROMAccessor(string ROMPath)
        {
            this.ROM = File.ReadAllBytes(ROMPath);

            this.Memory = new byte[ROMAccessor.memorySize];

            this.LoadSnapshot();

            this.MemoryReader = new PokeRedStreamReader(new MemoryStream(this.Memory), this);

            this.LoadDefaultBanks();
        }

        /// <summary>
        /// Loads the default banks when the game is loaded. That is, first half is bank 0 and second half is bank 1.
        /// </summary>
        public void LoadDefaultBanks()
        {
            this.LoadBank0();
            this.LoadBankN(0x01);
        }

        /// <summary>
        /// Loads bank 0 into memory.
        /// </summary>
        public void LoadBank0()
        {
            this.LoadBankNTo(0x00, 0x0000);
            this.FirstBankLoaded = 0;
        }

        /// <summary>
        /// Loads bank N to the second half of the ROM memory.
        /// </summary>
        /// <param name="N">Bank to be loaded</param>
        public void LoadBankN(byte N)
        {
            this.LoadBankNTo(N, ROMAccessor.bankSize);
            this.SecondBankLoaded = N;
        }

        /// <summary>
        /// Loads bank N to wherever address is specified.
        /// </summary>
        /// <param name="N">Bank to be loaded</param>
        /// <param name="address">Memory address to begin the data loading</param>
        public void LoadBankNTo(byte N, UInt16 address)
        {
            if (N > 0x2C) N = 1;

            for (UInt16 i = 0; i < ROMAccessor.bankSize; i++)
            {
                this.Memory[address + i] = this.ROM[(N * ROMAccessor.bankSize) + i];
            }
        }

        /// <summary>
        /// Loads a snapshot so every memory location has a realistic value.
        /// </summary>
        /// <param name="path">File with the dump. Must be 64KB in size.</param>
        public void LoadSnapshot(string path = "pokered.dump")
        {
            this.Memory = File.ReadAllBytes(path);
        }

        /// <summary>
        /// Returns the game name, written in bank 0.
        /// </summary>
        /// <returns></returns>
        public string GetGameName()
        {
            string ret = "";

            for(UInt16 i = 0; i < ROMAccessor.nameSize; i++)
            {
                var fetched = this.Memory[ROMAccessor.nameAddress + i];
                ret += (char)fetched;

                //If the fetched character is a NULL byte, stop reading.
                if (fetched == 0x0) break;
            }

            return ret;
        }

        /// <summary>
        /// Gets the offsets table used for Run Length Enconding used in sprites decompression.
        /// </summary>
        /// <returns></returns>
        public UInt16[] GetRLEOffsetTable()
        {
            //Located at Bank 0x00, 0x269F

            this.MemoryReader.BaseStream.Position = 0x269F;

            var ret = new UInt16[16];
            for(int i = 0; i < 16; i++)
            {
                ret[i] = this.MemoryReader.ReadUInt16();
            }

            return ret;
        }

        /// <summary>
        /// Gets the list of pokémon and move types.
        /// </summary>
        /// <returns></returns>
        public string[] GetTypes()
        {
            //Located at Bank 0x09, 0x7DAE

            this.LoadBankN(0x09);
            this.MemoryReader.BaseStream.Position = 0x7DAE;

            return this.MemoryReader.ReadGameStringPointerTable();
        }

        /// <summary>
        /// Get the type name of the ID specified.
        /// </summary>
        /// <returns></returns>
        public string GetType(byte id)
        {
            //Located at Bank 0x09, 0x7DAE

            this.LoadBankN(0x09);
            this.MemoryReader.BaseStream.Position = 0x7DAE + (id * 2);

            this.MemoryReader.BaseStream.Position = this.MemoryReader.ReadUInt16();

            return this.MemoryReader.ReadGameString();
        }

        /// <summary>
        /// Gets the dex number of a pokémon based on its ID.
        /// </summary>
        /// <param name="id">Pokémon's ID.</param>
        /// <returns></returns>
        public byte GetDexFromId(byte id)
        {
            //Located at Bank 0x10, 0x5024

            this.LoadBankN(0x10);
            this.MemoryReader.BaseStream.Position = 0x5024 + (byte)(id - 1);

            return this.MemoryReader.ReadByte();
        }

        /// <summary>
        /// Gets the ID of a pokémon based on its ID number. Note that the game never NEVER does this.
        /// Pokémon are always references by their ID, and the dex number is derived from it.
        /// This is just a reverse search.
        /// </summary>
        /// <param name="dex">Pokémon's dex number.</param>
        /// <returns></returns>
        public byte GetIdFromDex(byte dex)
        {
            //Located at Bank 0x10, 0x5024

            this.LoadBankN(0x10);
            this.MemoryReader.BaseStream.Position = 0x5024;

            for(byte i = 0; i <= 0xFF; i++)
            {
                var ret = this.MemoryReader.ReadByte();
                if (ret == dex) return (byte)(i + 1);
            }

            //If not found, throw an exception
            throw new KeyNotFoundException("Pokémon with dex number " + dex + " does not exist.");
        }

        /// <summary>
        /// Gets the list of pokémon names.
        /// </summary>
        /// <returns></returns>
        public string[] GetMonNames()
        {
            //Located at Bank 0x07, 0x421E

            this.LoadBankN(0x07);
            this.MemoryReader.BaseStream.Position = 0x421E;

            var ret = this.MemoryReader.ReadGameStringTable(0x0A);

            //This table starts at 1, so it must be shifted
            ret = ArrayShifter.ShiftDown(ret);

            return ret;
        }

        /// <summary>
        /// Gets a pokémon's name.
        /// </summary>
        /// <param name="id">ID of the pokémon.</param>
        /// <returns></returns>
        public string GetMonName(byte id)
        {
            //Located at Bank 0x07, 0x421E

            this.LoadBankN(0x07);
            this.MemoryReader.BaseStream.Position = 0x421E + (0x0A * (byte)(id - 1));

            return this.MemoryReader.ReadGameString(0x0A);
        }

        /// <summary>
        /// Gets the list of every pokémon's base stats
        /// </summary>
        /// <returns></returns>
        public MonBaseStats[] GetMonBaseStats()
        {
            //Located at Bank 0x0E, 0x43DE

            this.LoadBankN(0x0E);
            this.MemoryReader.BaseStream.Position = 0x43DE;

            var ret = new MonBaseStats[0xFF + 1];

            for(int i = 0; i <= 0xFF; i++)
            {
                ret[i] = new MonBaseStats().DeserializeFromStream(this.MemoryReader);
            }

            //This table starts at 1, so it must be shifted
            ret = ArrayShifter.ShiftDown(ret);

            //Mew (#151) has its base stats separated from every other pokémon
            this.MemoryReader.BaseStream.Position = 0x7644;
            ret[151] = new MonBaseStats().DeserializeFromStream(this.MemoryReader);

            return ret;
        }

        /// <summary>
        /// Get one specific pokémon's base stats.
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public MonBaseStats GetMonBaseStats(byte position)
        {
            //If it's Mew (#151), get the stats from its special place
            if(position == 151)
            {
                this.LoadBankN(0x01);
                this.MemoryReader.BaseStream.Position = 0x425B;

                return new MonBaseStats().DeserializeFromStream(this.MemoryReader);
            }

            //Located at Bank 0x0E, 0x43DE

            this.LoadBankN(0x0E);
            this.MemoryReader.BaseStream.Position = 0x43DE + (0x1C * ((byte)(position - 1)));

            return new MonBaseStats().DeserializeFromStream(this.MemoryReader);
        }

        /// <summary>
        /// Gets every pokédex entry.
        /// </summary>
        /// <returns></returns>
        public DexEntry[] GetDexEntries()
        {
            //Located at Bank 0x10, 0x447E

            this.LoadBankN(0x10);
            this.MemoryReader.BaseStream.Position = 0x447E;

            var ret = new DexEntry[0xFF + 1];

            var pointerTable = this.MemoryReader.ReadPointerTable();

            for(int i = 0; i <= 0xFF; i++)
            {
                this.MemoryReader.BaseStream.Position = pointerTable[i];
                ret[i] = new DexEntry().DeserializeFromStream(this.MemoryReader);
            }

            //This table starts at 1, so it must be shifted
            ret = ArrayShifter.ShiftDown(ret);

            return ret;
        }

        /// <summary>
        /// Gets the pokédex entry of a pokémon based on its ID.
        /// </summary>
        /// <param name="id">ID of the pokémon.</param>
        /// <returns></returns>
        public DexEntry GetDexEntry(byte id)
        {
            //Located at Bank 0x10, 0x447E

            this.LoadBankN(0x10);
            this.MemoryReader.BaseStream.Position = 0x447E + (2 * (byte)(id - 1));

            this.MemoryReader.BaseStream.Position = this.MemoryReader.ReadUInt16();

            return new DexEntry().DeserializeFromStream(this.MemoryReader);
        }

        /// <summary>
        /// Gets the name of a move based on its ID.
        /// </summary>
        /// <param name="id">Move's ID.</param>
        /// <returns></returns>
        public string GetMoveName(byte id)
        {
            //Located at Bank 0x2C, 0x4000

            this.LoadBankN(0x2C);
            this.MemoryReader.BaseStream.Position = 0x4000;

            //Moves names are stored as variable-length strings with a terminator, all in a row.
            //The first one has ID 1 and are not indexed, so we have to run through every name until
            //we find the one we want. This is how the game actually does it!

            //Move 0 does not exist. It marks that a move slot is empty.
            if(id == 0)
            {
                return "<EMPTY>";
            }

            string ret = "";

            for(int i = 0; i < id; i++)
            {
                ret = this.MemoryReader.ReadGameString(null, false, true);
            }

            return ret;
        }

        /// <summary>
        /// Gets the move ID a TM or HM has.
        /// </summary>
        /// <param name="tmhm">The ID the TM or HM has.</param>
        /// <returns></returns>
        public byte GetTMHMMoveId(byte tmhm)
        {
            //Located at Bank 0x04, 0x7773

            this.LoadBankN(0x04);
            this.MemoryReader.BaseStream.Position = 0x7773 + (tmhm - 1);

            return this.MemoryReader.ReadByte();
        }

        /// <summary>
        /// Gets the move name a TM or HM has.
        /// </summary>
        /// <param name="tmhm">The ID the TM or HM has.</param>
        /// <returns></returns>
        public string GetTMHMMoveName(byte tmhm)
        {
            //First get move ID related to that TM or HM
            var moveId = this.GetTMHMMoveId(tmhm);

            //Then return its name
            return this.GetMoveName(moveId);
        }

        /// <summary>
        /// Gets the color palette ID of a pokémon.
        /// </summary>
        /// <param name="dexNumber"></param>
        /// <returns></returns>
        public byte GetMonPaletteId(byte dexNumber)
        {
            //Located at Bank 0x1C, 0x65C8

            this.LoadBankN(0x1C);
            this.MemoryReader.BaseStream.Position = 0x65C8 + (dexNumber);

            return this.MemoryReader.ReadByte();
        }

        /// <summary>
        /// Gets the color palette corresponding to that index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public Color[] GetPalette(byte index)
        {
            //Located at Bank 0x1C, 0x6660

            this.LoadBankN(0x1C);
            this.MemoryReader.BaseStream.Position = 0x6660 + (index * 8);

            //Each palette has 4 colors
            Color[] ret = new Color[4];

            //Each color in the palette is stored as 16 bits, 5 bits per color and one bit to spare.
            for(int i = 0; i < 4; i++)
            {
                UInt16 c = this.MemoryReader.ReadUInt16();
                var r = c & 0b0000000000011111;
                var g = (c & 0b0000001111100000) >> 5;
                var b = (c & 0b0111110000000000) >> 10;
                ret[i] = Color.FromArgb(
                    r * 0xFF / 31,
                    g * 0xFF / 31,
                    b * 0xFF / 31
                );
            }

            return ret;
        }

        /// <summary>
        /// Gets the palette of a pokémon based on its dex number.
        /// </summary>
        /// <param name="dexNumber"></param>
        /// <returns></returns>
        public Color[] GetMonPalette(byte dexNumber)
        {
            return this.GetPalette(this.GetMonPaletteId(dexNumber));
        }

        /// <summary>
        /// Gets the list of possible evolutions and learnable moves of a pokémon.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public EvosMoves GetMonEvosMoves(byte id)
        {
            //Located at Bank 0x0E, 0x705C

            this.LoadBankN(0x0E);
            this.MemoryReader.BaseStream.Position = 0x705C + (2 * (byte)(id - 1));

            //Jump to memory address found in pointer table
            this.MemoryReader.BaseStream.Position = this.MemoryReader.ReadUInt16();

            var ret = new EvosMoves();
            return ret.DeserializeFromStream(this.MemoryReader);
        }

        /// <summary>
        /// Gets an item's name based on its ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public String GetItemName(byte id)
        {
            //Located at Bank 0x01, 0x472B

            this.LoadBankN(0x01);
            this.MemoryReader.BaseStream.Position = 0x472B;

            //Item names are stored as variable-length strings with a terminator, all in a row.
            //The first one has ID 1 and are not indexed, so we have to run through every name until
            //we find the one we want. This is how the game actually does it!

            //Item 0 does not exist.
            if (id == 0)
            {
                return "";
            }

            string ret = "";

            for (int i = 0; i < id; i++)
            {
                ret = this.MemoryReader.ReadGameString(null, false, true);
            }

            return ret;
        }

        public String GetGrowthFormula(byte id)
        {
            //Located at Bank 0x16, 0x501D

            this.LoadBankN(0x16);
            this.MemoryReader.BaseStream.Position = 0x501D + (4 * id);

            //FORMULA: (A)/(B) * N^3 + (C) * N^2 + (D) * N - (E)
            byte B = this.MemoryReader.ReadNibble(NibbleLevel.LOW);
            byte A = this.MemoryReader.ReadNibble(NibbleLevel.HIGH);
            byte C = this.MemoryReader.ReadByte();
            byte D = this.MemoryReader.ReadByte();
            byte E = this.MemoryReader.ReadByte();

            var parts = new List<string>();

            //PART 1
            if(A != 0 && B != 0)
            {
                if (A == B) parts.Add("n^3");
                else parts.Add(String.Format("{0}/{1} n^3", A, B));
            }

            //PART 2
            if(C != 0)
            {
                int trueC = C;
                if((C & 0b10000000) >> 7 == 1){
                    trueC &= 0b01111111;
                    trueC = -trueC;
                }

                if (trueC == 1) parts.Add("n^2");
                else parts.Add(String.Format("{0} n^2", trueC));
            }

            //PART 3
            if (D != 0)
            {
                if (D == 1) parts.Add("n");
                else parts.Add(String.Format("{0} n", D));
            }

            //PART 4
            if (E != 0)
            {
                parts.Add(String.Format("- {0}", E));
            }

            string ret = "";
            foreach(string part in parts)
            {
                if(ret != "")
                {
                    if (part.Contains("-")) ret += " - ";
                    else ret += " + ";
                }

                ret += part.Replace("-", "").Trim();
            }

            return ret;
        }
    }
}
