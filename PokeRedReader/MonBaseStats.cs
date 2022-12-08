using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeRedReader
{
    public class MonBaseStats
    {
        public byte
            DexNumber,
            HP,
            ATK,
            DEF,
            SPD,
            SPC,
            Type1,
            Type2,
            CatchRate,
            BaseExp,
            SpriteDimensionX,
            SpriteDimensionY
        ;

        public UInt16
            FrontSpritePointer,
            BackSpritePointer
        ;

        public byte
            Move1,
            Move2,
            Move3,
            Move4,
            GrowthTable
        ;

        public bool[] TMHM;

        public byte Padding;

        /// <summary>
        /// There are a total of 50 TMs and 5 HMs, all fitting in 7 bytes with one flag unused.
        /// </summary>
        public static readonly int TOTAL_TMHM = 7 * 8;

        public MonBaseStats()
        {
            this.TMHM = new bool[MonBaseStats.TOTAL_TMHM];
        }

        /// <summary>
        /// Deserializes from a PokeRedStreamReader object and returns it.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public MonBaseStats DeserializeFromStream(PokeRedStreamReader reader)
        {
            //Note that the reader must have its Position set to the start of the data
            this.DexNumber = reader.ReadByte();
            this.HP = reader.ReadByte();
            this.ATK = reader.ReadByte();
            this.DEF = reader.ReadByte();
            this.SPD = reader.ReadByte();
            this.SPC = reader.ReadByte();
            this.Type1 = reader.ReadByte();
            this.Type2 = reader.ReadByte();
            this.CatchRate = reader.ReadByte();
            this.BaseExp = reader.ReadByte();
            this.SpriteDimensionX = reader.ReadNibble(NibbleLevel.LOW);
            this.SpriteDimensionY = reader.ReadNibble(NibbleLevel.HIGH);
            this.FrontSpritePointer = reader.ReadUInt16();
            this.BackSpritePointer = reader.ReadUInt16();
            this.Move1 = reader.ReadByte();
            this.Move2 = reader.ReadByte();
            this.Move3 = reader.ReadByte();
            this.Move4 = reader.ReadByte();
            this.GrowthTable = reader.ReadByte();
            this.TMHM = reader.ReadFlags((byte)MonBaseStats.TOTAL_TMHM);
            this.Padding = reader.ReadByte();

            return this;
        }

    }
}
