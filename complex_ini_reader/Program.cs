using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;

namespace complex_ini_reader
{
    class Program
    {

        public static settings_class SETTINGS = new settings_class();


        /*public struct SETTINGS_t
        {
            public string str_param;
            public bool bool_param;
            public int int_param;
            public long long_param;
            public ulong ulong_param;
            public uint uint_param;

            public uint[] uint_array_param;

            public int[] int_array_param;

            
        }*/

        /*
         * вариант пройтись по типам без switch и вынужденного преобразования в строку
         var key_type = smsg[key].GetType();

                if (key_type == typeof(ru.micexrts.cgate.message.ValueCXX))
                {
                    if (key.ToLower() == "strdateexp")
                    {
                        smsg[key].set(val.Replace("/", String.Empty).Substring(0, 8));
                    }
                    else smsg[key].set(val);
                }
                else if (key_type == typeof(ru.micexrts.cgate.message.ValueI2))
                {
                    smsg[key].set(short.Parse(val));

                }
         */
        

         private static void usage(string msg)
        {
            if(msg.Length>0)
            Console.Error.WriteLine("ERROR: {0}",msg);

            Console.Error.WriteLine(
@"Can read files
To run:
One or more log file(s), plain text
bla bla bla");
;
         }


        static void Main(string[] args)
        {
            //var SETTINGS = new SETTINGS_t();

            string[] optArgs;

            try
            {
                SETTINGS.init(args, out optArgs);
            }
            catch// (Exception Ex)
            {
                usage("read params from ini file or cmd failed");
                Console.Error.WriteLine("params for ini or cmd are:\n{0}", SETTINGS.template());
                //Console.Error.WriteLine("\n{0}", Ex.ToString());
                throw;


            }
            
            
            SETTINGS.auto(1);
            //SETTINGS.read();


            Console.Error.WriteLine("fresh params:\n{0}", SETTINGS.ToString());

            //SETTINGS.print
            //settings_class.template();

            while (true)
            {
                System.Threading.Thread.Sleep(1000);
                Console.Error.WriteLine(".");
                Console.Error.WriteLine("val for int_param is {0}", SETTINGS.int_param);
                Console.Error.WriteLine(SETTINGS.ToString());

            }

            //Console.Error.WriteLine("{0}", INI.abc);

            

            Console.Error.WriteLine(SETTINGS.ToString());
            Console.Error.WriteLine("+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
            Console.Error.WriteLine("RETURN VAL: {0}",SETTINGS.read());

            Console.Error.WriteLine(SETTINGS.ToString());
            //print_ini(SETTINGS);
            
            //print_ini(SETTINGS);
            
        }
    }
}
