using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeRedReader
{
    public class DexEntry
    {
        public string Species;
        public byte
            HeightFeet, HeightInches
        ;
        public float Weight;
        public string Text;

        public DexEntry() {}

        /// <summary>
        /// Deserializes from a PokeRedStreamReader object and returns it.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public DexEntry DeserializeFromStream(PokeRedStreamReader reader)
        {
            //Note that the reader must have its Position set to the start of the data
            this.Species = reader.ReadGameString();
            this.HeightFeet = reader.ReadByte();
            this.HeightInches = reader.ReadByte();
            this.Weight = (float)reader.ReadUInt16() / 10;
            this.Text = reader.ReadGameString();

            //Format text correctly
            this.Text = this.Text.Replace("<NULL>", "");
            this.Text = this.Text.Replace("<DEXEND>", "");
            this.Text = this.Text.Replace("<NEXT>", " ");
            this.Text = this.Text.Replace("<PAGE>", " ");

            return this;
        }
    }
}
