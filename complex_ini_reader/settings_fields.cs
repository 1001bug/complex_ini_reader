using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace complex_ini_reader
{
    partial class settings_class
    {

        //рефлект автомат видит только public
        //стоит пометить как private - ее нельзя ни увидет ни назначить

        public string str_param="some string val (Class)";
        public bool bool_param = true;
        public int int_param = -1234;
        public long long_param=-100500111;
        public ulong ulong_param=100500111;
        public uint uint_param=1234;
        public uint[] uint_array_param=new UInt32[] { 1, 2, 3,4 };
        public int[] int_array_param=new Int32[] { -1, -2, -3,4 };

    }
}
