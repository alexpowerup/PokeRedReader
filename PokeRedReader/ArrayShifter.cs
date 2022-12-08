using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeRedReader
{
    public class ArrayShifter
    {
        public static T[] ShiftDown<T>(T[] input, int times = 1)
        {
            T[] ret = new T[input.Length];

            for(int i = 0; i < input.Length; i++)
            {
                ret[(i + input.Length + times % input.Length) % input.Length] = input[i];
            }

            return ret;
        }
    }
}
