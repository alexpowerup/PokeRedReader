using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeRedReader
{
    public class EvosMoves
    {
        public List<Evolution> Evolutions;

        public List<MoveByLevel> Learnset;

        public EvosMoves()
        {
            this.Evolutions = new List<Evolution>();
            this.Learnset = new List<MoveByLevel>();
        }

        /// <summary>
        /// Deserializes from a PokeRedStreamReader object and returns it.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public EvosMoves DeserializeFromStream(PokeRedStreamReader reader)
        {
            //Note that the reader must have its Position set to the start of the data

            //The evolutions list is read first
            byte method = 0;
            Evolution tmp;
            do
            {
                tmp = new Evolution();
                method = reader.ReadByte();
                switch (method)
                {
                    case 0: //End of the list
                        break;
                    case 1: //LEVEL
                        tmp.Method = EvolutionMethod.LEVEL;
                        tmp.MinLevel = reader.ReadByte();
                        tmp.EvolveTo = reader.ReadByte();
                        this.Evolutions.Add(tmp);
                        break;
                    case 2: //ITEM
                        tmp.Method = EvolutionMethod.ITEM;
                        tmp.Item = reader.ReadByte();
                        tmp.MinLevel = reader.ReadByte();
                        tmp.EvolveTo = reader.ReadByte();
                        this.Evolutions.Add(tmp);
                        break;
                    case 3: //TRADE
                        tmp.Method = EvolutionMethod.TRADE;
                        tmp.MinLevel = reader.ReadByte();
                        tmp.EvolveTo = reader.ReadByte();
                        this.Evolutions.Add(tmp);
                        break;
                    default: //In any other case, read two bytes and continue
                        reader.ReadByte();
                        reader.ReadByte();
                        break;
                }
            } while (method != 0);

            //Learnset is written after
            byte level = 0;
            MoveByLevel tmpMove;
            do
            {
                tmpMove = new MoveByLevel();
                level = reader.ReadByte();
                if(level > 0)
                {
                    tmpMove.Level = level;
                    tmpMove.Move = reader.ReadByte();
                    this.Learnset.Add(tmpMove);
                }
            } while (level != 0);

            return this;
        }
    }

    public class Evolution
    {
        public EvolutionMethod Method;
        public byte MinLevel;
        public byte? Item;
        public byte EvolveTo;
    }

    public enum EvolutionMethod
    {
        LEVEL = 1,
        ITEM = 2,
        TRADE = 3
    }

    public class MoveByLevel
    {
        public byte Level;
        public byte Move;
    }
}
