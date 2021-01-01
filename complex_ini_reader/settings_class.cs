using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Timers;
using System.Text.RegularExpressions;

//this file is universal for every project
//put custom in settings_field.cs

/*
 * может без ini файла, только на параметрах вызова, но тогда хотябы один должен быть указан. иначе не понять чего хотели
 * program.exe path_to_ini -param=val -param=val
 * проецирование идет от параметров пользователя в поля класса. если такого поля в классе нет, а в парамтрах запуска есть - будет ошибка
 * комментарий ; или #
 * пустой файл ошибок не вызывает
 * имена чувствительны к реигистру
 * в файле просто имена, а в параметрах вызова с минусом
 * в файле вокрур = могут быть пробелы в любом количестве
 * в параметрах выхова вокруг = пробелов не должно быть
 * рефакторинг имени поля класса потребует ручной правки в ini файле, но везде в коде программы все исправиться автоматом
 * в одной программе не поучится иметь несколько экземпляров. технически ограничений нет, но т.к. поля хардкодятся внутри класса это не имеет смысла
 * 
 * ожидается такое использование
 * глобальная переменная в главном классе программы
 * public static settings_class SETTINGS = new settings_class();
 * 
 * в main() вызывается init() на экземпляре
 * обарбатываются Exceptions если что-то не так с параметрами (если указан путь к ini файлу а его нет или кривые переопределяющие параметры)
 * 
 * следом в main() вызывается read() для непосредственного чтения или auto() чтобы происитать и потом оно само перечитывалось
 * обарбатываются Exceptions если что-то нетак с параметрами из объединенного списка файл+переопределнения (с парсингом и приведением типов)
 * 
 * в процессе init() проверяются только имена парметров, что они есть такие в полях класса
 * в процессе read() проверяется именя парметров из ini и преведение к нужному типу объединеного списка.
 * технически в init запоминатеся список параметр=значение в виде текста а в процессе read() он накладывается на то что прочиталось из ini и уже этот объединенный список проецируется на поля класса
 * */


namespace complex_ini_reader
{
    partial class settings_class
    {

        private string Path = null;
        private DateTime LastWriteTime = DateTime.MinValue;
        private Dictionary<string, string> overrieds = new Dictionary<string, string>();
        private Object Locker = new Object();
        private System.Timers.Timer interval_read;
        private Boolean inited = false;
        private Regex R_pattern_number_h = new Regex(@"^(-?\d+)([KMG])?$", RegexOptions.Compiled);

        /// <summary>
        /// get keys and values as multi string text
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return asMultiString();
        }

        //принимает строку параметров main, ждет что первый - это путь к ini файлу, потом переопределяющие ключи. Отдает объекс ini а в optArgs остаются параметры НЕ ключи
        /// <summary>
        /// init calass with ini main() args. then do auto() for interval reading or read() for one-time ini read
        /// </summary>
        /// <param name="Args">main() args</param>
        /// <param name="optArgs">main() args without ini file path and additional override params</param>
        public void init(string[] Args, out string[] optArgs/*, Action<string> usage*/)
        {
            if (Args.Length < 1 || Args[0].Equals("-h") || Args[0].Equals("--help") || Args[0].Equals("/?"))
            {
                //usage("Need help?");
                //Console.Error.WriteLine("INI teplate:\n{0}",template());
                throw new SystemException(string.Format("Neither INI file path nor override params not set"));
            }

            List<string> other = null;

            if (Args[0].StartsWith("-"))
            {
                Console.Error.WriteLine("ini file not set, all parameters from cmd");
                other = Args.ToList();
            }
            else
            {
                string Path = Args[0];
                if (!File.Exists(Path))
                    throw new SystemException(string.Format("INI file not found: '{0}'", Path));

                this.Path = Path;
                other = Args.Skip(1).ToList();
            }



            //override by run params
            foreach (var arg in other.Where(w => w.StartsWith("-") /*&& w.Contains("=")*/))
            {

                var X = arg.TrimStart(new[] { '-' }).Split(new[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (X.Length == 2)
                {
                    string key = X[0].Trim();
                    string val = X[1].Trim();
                    FieldInfo fld = this.GetType().GetField(key);
                    if (fld == null)
                        throw new SystemException(string.Format("override INI param {0} not known: {0}={1}", key, val));
                    overrieds[key] = val;

                }
                else
                    throw new SystemException(string.Format("override INI param wrong format at: {0}", arg));
            }

            //override by ENV
            foreach (System.Collections.DictionaryEntry kvp in Environment.GetEnvironmentVariables())
            {
                var key = kvp.Key.ToString();
                FieldInfo fld = this.GetType().GetField(key);
                if (fld != null)
                    overrieds[key] = kvp.Value.ToString();
            }

            optArgs = other.Where(w => !w.StartsWith("-") /*&& w.Contains("=")*/).ToArray();
            inited = true;
        }

        /// <summary>
        /// Read ini and set Timer to repeat read every N milliseconds
        /// </summary>
        /// <param name="inerval_mls"></param>
        public void auto(double inerval_mls = 1000)
        {
            if (!inited)
                throw new SystemException(string.Format("init() first"));

            //first read ini
            read(false);

            if (this.Path == null)
                Console.Error.WriteLine(string.Format("INI path not set, auto() is useless"));
            else
            {
                //then set interval rereading
                interval_read = new System.Timers.Timer(inerval_mls);
                interval_read.Elapsed += sequence_action;
                interval_read.AutoReset = true;
                interval_read.Enabled = false;
                interval_read.Start();
            }

        }

        private void sequence_action(Object source, ElapsedEventArgs e)
        {
            try
            {
                read();
            }
            catch (Exception Exxx)
            {
                Console.Error.WriteLine("++++++++++ Sequence read ini failed ++++++++++\n{0}", Exxx.ToString());
            }
        }
        /// <summary>
        /// Read ini file once
        /// </summary>
        /// <returns>true if OK</returns>
        public bool read(Boolean set_output = true)
        {
            if (!inited)
                throw new SystemException(string.Format("init() first"));
            //выдает Exceprion на нераспознанные параметры
            //если вызвать нарямую или через auto() то надо обрабатывать Exceprion
            //последующие перечитывания в рамках sequence_action Exceprion будет просто печататься в stderr
            //а то испортишь ini в процессе  работы проги и она сложится (хотя не уверен, как там sequence_action устроен...)

            lock (Locker)
            {
                //var a1232 = this.GetType().GetProperties();
                //var a1232 = this.GetType().GetFields();







                //читаем файл
                //делаем словарь ключ=знгачение
                //накладываем на него овверайды
                //бежим по ключам и ищем в SETTINGS таковой
                //если в INI есть парметр которого нет в SETTINGS - будет ошибка. т.е. ошибочный или лишний параметр всегда всплывет

                var settings = new Dictionary<string, string>();

                //читаем ini олько если он есть. иначе тольок оверрайд параметры
                if (this.Path != null)
                {
                    DateTime t = File.GetLastWriteTime(this.Path);
                    if (LastWriteTime == t) return false;

                    LastWriteTime = t;

                    string[] ini_file_content = ReadAllLinesShared(this.Path);

                    foreach (var line in ini_file_content.Where(w => w.Length > 2 && !w.StartsWith("#") && !w.StartsWith(";") && !w.StartsWith(" ")))
                    {
                        var X = line.Split(new[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
                        //получается невозможно задать пустое значение. а надо?
                        if (X.Length == 2)
                        {
                            //проверка на дубликат
                            string key = X[0].Trim();
                            if (settings.ContainsKey(key))
                                throw new SystemException(string.Format("INI file contains dup Key {0} format at '{1}'", key, line));

                            settings[key] = X[1].Trim();
                        }
                        else
                            throw new SystemException(string.Format("INI file wrong format at '{0}'", line));
                    }
                }
                //override by run params
                foreach (var kvp in overrieds)
                {
                    settings[kvp.Key] = kvp.Value;
                }

                if (settings.Count == 0) return false;



                foreach (var kvp in settings)
                {
                    try
                    {
                        FieldInfo fld = this.GetType().GetField(kvp.Key);
                        if (fld == null)
                            throw new SystemException(string.Format("key {0} not known: {0}={1}", kvp.Key, kvp.Value));

                        //Console.WriteLine("!!! {0}={1} => {0}={2}", kvp.Key, fld.GetValue(SETTINGS), kvp.Value);

                        var FieldType = fld.FieldType.ToString();

                        //из-за того что метод SetValue делает копию, приходится вот такой костыль применять
                        //этого не надо если SETTINGS не структуа а класс, но это много пределывать...
                        //object S = SETTINGS;

                        switch (FieldType)
                        {
                            case "System.String":
                                {
                                    var old_val = fld.GetValue(this);
                                    string new_val = kvp.Value;
                                    if (!new_val.Equals(old_val))
                                    {
                                        if (set_output) Console.Error.WriteLine("Set {0} = '{1}'", kvp.Key, new_val);
                                        fld.SetValue(this, new_val);
                                    }
                                    break;
                                }
                            case "System.Boolean":
                                {
                                    var old_val = fld.GetValue(this);
                                    var new_val = false;
                                    switch (kvp.Value.ToLower())
                                    {
                                        case "true":
                                        case "yes":
                                        case "on":
                                            //fld.SetValue(this, true);
                                            new_val = true;
                                            break;
                                        case "false":
                                        case "no":
                                        case "off":
                                            //fld.SetValue(this, false);
                                            new_val = false;
                                            break;
                                        default:
                                            throw new SystemException(string.Format("Boolean val for {0} not known: {1}; Possible values are: true|yes|on|false|no|off", kvp.Key, kvp.Value)); ;
                                    }
                                    if (!new_val.Equals(old_val))
                                    {
                                        if (set_output) Console.Error.WriteLine("Set {0} = '{1}'", kvp.Key, new_val);
                                        fld.SetValue(this, new_val);
                                    }

                                    break;
                                }
                            case "System.Int32":
                                {
                                    var old_val = fld.GetValue(this);
                                    Int32 new_val;

                                    if (!TryParse(kvp.Value, out new_val))
                                        throw new SystemException(string.Format("Int32 val for {0} not known: {1}; Possible values are: {2}..{3}. Postfix: K|M|G", kvp.Key, kvp.Value, Int32.MinValue, Int32.MaxValue)); ;

                                    if (!new_val.Equals(old_val))
                                    {
                                        if (set_output) Console.Error.WriteLine("Set {0} = '{1}'", kvp.Key, new_val);
                                        fld.SetValue(this, new_val);
                                    }
                                }
                                break;
                            case "System.Int64":
                                {
                                    var old_val = fld.GetValue(this);
                                    Int64 new_val;

                                    if (!TryParse(kvp.Value, out new_val))
                                        throw new SystemException(string.Format("Int64 val for {0} not known: {1}; Possible values are: {2}..{3}. Postfix: K|M|G", kvp.Key, kvp.Value, Int64.MinValue, Int64.MaxValue)); ;

                                    if (!new_val.Equals(old_val))
                                    {
                                        if (set_output) Console.Error.WriteLine("Set {0} = '{1}'", kvp.Key, new_val);
                                        fld.SetValue(this, new_val);
                                    }
                                }
                                break;
                            case "System.UInt32":
                                {
                                    var old_val = fld.GetValue(this);
                                    UInt32 new_val;

                                    if (!TryParse(kvp.Value, out new_val))
                                        throw new SystemException(string.Format("UInt32 val for {0} not known: {1}; Possible values are: {2}..{3}. Postfix: K|M|G", kvp.Key, kvp.Value, UInt32.MinValue, UInt32.MaxValue)); ;

                                    if (!new_val.Equals(old_val))
                                    {
                                        if (set_output) Console.Error.WriteLine("Set {0} = '{1}'", kvp.Key, new_val);
                                        fld.SetValue(this, new_val);
                                    }
                                }
                                break;
                            case "System.UInt64":
                                {
                                    var old_val = fld.GetValue(this);
                                    UInt64 new_val;

                                    if (!TryParse(kvp.Value, out new_val))
                                        throw new SystemException(string.Format("UInt64 val for {0} not known: {1}; Possible values are: {2}..{3}. Postfix: K|M|G", kvp.Key, kvp.Value, UInt64.MinValue, UInt64.MaxValue)); ;
                                    if (!new_val.Equals(old_val))
                                    {
                                        if (set_output) Console.Error.WriteLine("Set {0} = '{1}'", kvp.Key, new_val);
                                        fld.SetValue(this, new_val);
                                    }
                                }
                                break;
                            case "System.UInt32[]":
                                {
                                    UInt32[] tmp = kvp.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(dig =>
                                    {
                                        UInt32 out_val;
                                        if (UInt32.TryParse(dig, out out_val)) return out_val;
                                        else
                                            throw new SystemException(string.Format("UInt32[] string for {0} parsing error: {0}={1}; Expecting format: 1,2,3,4", kvp.Key, kvp.Value));
                                    }
                                            ).ToArray();
                                    fld.SetValue(this, tmp);
                                }
                                break;
                            case "System.Int32[]":
                                {
                                    Int32[] tmp = kvp.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(dig =>
                                    {
                                        Int32 out_val;
                                        if (Int32.TryParse(dig, out out_val)) return out_val;
                                        else
                                            throw new SystemException(string.Format("Int32[] string for {0} parsing error: {0}={1}; Expecting format: 1,-2,3,-4", kvp.Key, kvp.Value));
                                    }
                                            ).ToArray();
                                    fld.SetValue(this, tmp);
                                }
                                break;
                            case "System.Int16":
                                {
                                    var old_val = fld.GetValue(this);
                                    Int16 new_val;
                                    //обычный парсинг без постфиксов, т.к. максимум 32К
                                    if (!Int16.TryParse(kvp.Value, out new_val))
                                        throw new SystemException(string.Format("Int16 val for {0} not known: {1}; Possible values are: {2}..{3}", kvp.Key, kvp.Value, Int16.MinValue, Int16.MaxValue)); ;

                                    if (!new_val.Equals(old_val))
                                    {
                                        if (set_output) Console.Error.WriteLine("Set {0} = '{1}'", kvp.Key, new_val);
                                        fld.SetValue(this, new_val);
                                    }
                                }
                                break;
                            case "System.UInt16":
                                {
                                    var old_val = fld.GetValue(this);
                                    UInt16 new_val;

                                    //обычный парсинг без постфиксов, т.к. максимум 65К
                                    if (!UInt16.TryParse(kvp.Value, out new_val))
                                        throw new SystemException(string.Format("UInt32 val for {0} not known: {1}; Possible values are: {2}..{3}", kvp.Key, kvp.Value, UInt16.MinValue, UInt16.MaxValue)); ;

                                    if (!new_val.Equals(old_val))
                                    {
                                        if (set_output) Console.Error.WriteLine("Set {0} = '{1}'", kvp.Key, new_val);
                                        fld.SetValue(this, new_val);
                                    }
                                }
                                break;
                            default:
                                throw new SystemException(string.Format("\tTYPE not known {0}", FieldType)); ;
                        }//swithc end



                        //Console.WriteLine("??? {0}={1} => {0}={2}", kvp.Key, fld.GetValue(SETTINGS), kvp.Value);

                    }
                    catch //(/*Exception Ex*/)
                    {
                        Console.Error.WriteLine("========== ini.read() general Exception inside foreach block ==============");
                        throw;
                    }
                }


                return true;
            }
        }

        private bool TryParse(string p, out Int32 new_val)
        {
            string number;
            string mult;

            if (!TrySplit(p, out number, out mult))
            {
                new_val = 0;
                return false;
            }

            try
            {
                checked
                {
                    new_val = Int32.Parse(number) * Int32.Parse(mult);
                }
                return true;
            }
            catch
            {
                new_val = 0;
                return false;
            }

        }


        private bool TryParse(string p, out UInt32 new_val)
        {
            string number;
            string mult;

            if (!TrySplit(p, out number, out mult))
            {
                new_val = 0;
                return false;
            }

            try
            {
                checked
                {
                    new_val = UInt32.Parse(number) * UInt32.Parse(mult);
                }
                return true;
            }
            catch
            {
                new_val = 0;
                return false;
            }

        }

        private bool TryParse(string p, out Int64 new_val)
        {
            string number;
            string mult;

            if (!TrySplit(p, out number, out mult))
            {
                new_val = 0;
                return false;
            }

            try
            {
                checked
                {
                    new_val = Int64.Parse(number) * Int64.Parse(mult);
                }
                return true;
            }
            catch
            {
                new_val = 0;
                return false;
            }

        }


        private bool TryParse(string p, out UInt64 new_val)
        {
            string number;
            string mult;

            if (!TrySplit(p, out number, out mult))
            {
                new_val = 0;
                return false;
            }

            try
            {
                checked
                {
                    new_val = UInt64.Parse(number) * UInt64.Parse(mult);
                }
                return true;
            }
            catch
            {
                new_val = 0;
                return false;
            }

        }

        private bool TrySplit(string p, out string number, out string mult)
        {
            number = "0";
            mult = "0";
            Match d_h = R_pattern_number_h.Match(p);
            if (d_h.Success)
            {
                number = d_h.Groups[1].ToString();
                switch (d_h.Groups[2].ToString())
                {
                    case "n": mult = "0.000000001"; break;
                    case "u": mult = "0.000001"; break;
                    case "m": mult = "0.001"; break;
                    case "": mult = "1"; break;
                    case "K": mult = "1000"; break; //вроде как должна быть маленькая k
                    case "M": mult = "1000000"; break;
                    case "G": mult = "1000000000"; break;

                    default: return false;
                }
                return true;

            }
            return false;


        }
        /// <summary>
        /// Refwite File.ReadAllLines to shared file access
        /// https://stackoverflow.com/questions/12744725/how-do-i-perform-file-readalllines-on-a-file-that-is-also-open-in-excel
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string[] ReadAllLinesShared(string path)
        {
            if (path == null)
                throw new ArgumentNullException("path");
            if (path.Length == 0)
                throw new ArgumentException("Path_Argument_Empty");
            else
                return InternalReadAllLinesShared(path, Encoding.UTF8);
        }
        /// <summary>
        /// Rewrite File.InternalReadAllLines to shared file access
        /// https://stackoverflow.com/questions/12744725/how-do-i-perform-file-readalllines-on-a-file-that-is-also-open-in-excel
        /// </summary>
        /// <param name="path"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        private string[] InternalReadAllLinesShared(string path, Encoding encoding)
        {
            List<string> list = new List<string>();
            using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader streamReader = new StreamReader(fileStream, encoding))

            //using (StreamReader streamReader = new StreamReader(path, encoding))
            {
                string str;
                while ((str = streamReader.ReadLine()) != null)
                    list.Add(str);
            }
            return list.ToArray();
        }

        /// <summary>
        /// return string with current key=value on each string
        /// </summary>
        /// <returns></returns>
        private string asMultiString()
        {

            StringBuilder output = new StringBuilder();

            foreach (var field in this.GetType().GetFields())
            {


                var FieldType = field.FieldType.ToString();
                switch (FieldType)
                {
                    case "System.String":
                        var val0 = (String)field.GetValue(this);
                        var VAL0 = val0 == null || val0.Length == 0 ? "(notset)" : val0;

                        output.AppendLine(String.Format("{0} = ({1}) '{2}'", field.Name, field.FieldType, VAL0));
                        break;
                    case "System.Boolean":
                    case "System.Int32":
                    case "System.Int64":
                    case "System.UInt32":
                    case "System.UInt64":
                    case "System.Int16":
                    case "System.UInt16":
                        output.AppendLine(String.Format("{0} = ({1}) '{2}'", field.Name, field.FieldType, field.GetValue(this)));
                        break;
                    case "System.UInt32[]":
                        var val = (System.UInt32[])field.GetValue(this);
                        var VAL = val == null ? "(notset)" : string.Join(",", val);

                        output.AppendLine(String.Format("{0} = ({1}) '{2}'", field.Name, field.FieldType, VAL));
                        break;
                    case "System.Int32[]":
                        var val1 = (System.Int32[])field.GetValue(this);
                        var VAL1 = val1 == null ? "(notset)" : string.Join(",", val1);

                        output.AppendLine(String.Format("{0} = ({1}) '{2}'", field.Name, field.FieldType, VAL1));
                        break;
                    default:
                        throw new SystemException(string.Format("TYPE {0} in SETTINGS not known", FieldType)); ;
                }//swithc end

            }//foreach end

            return output.ToString();
        }
        /// <summary>
        /// return string with ini file contens (template wtih harcoded values)
        /// </summary>
        /// <returns></returns>
        public string template()
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("#").AppendLine("#Parameters for INI file or CMD based on current values").AppendLine("#").AppendLine(String.Empty);



            foreach (var field in this.GetType().GetFields())
            {


                var FieldType = field.FieldType.ToString();
                switch (FieldType)
                {
                    case "System.String":
                        var val0 = (String)field.GetValue(this);
                        var VAL0 = val0 == null || val0.Length == 0 ? "(notset)" : val0;

                        output.AppendLine(String.Format("# {0}", field.FieldType));
                        output.AppendLine(String.Format("{0} = {1}", field.Name, VAL0)).AppendLine(String.Empty);
                        break;
                    case "System.Boolean":
                    case "System.Int32":
                    case "System.Int64":
                    case "System.UInt32":
                    case "System.UInt64":
                    case "System.Int16":
                    case "System.UInt16":
                        output.AppendLine(String.Format("# {0}", field.FieldType));
                        output.AppendLine(String.Format("{0} = {1}", field.Name, field.GetValue(this))).AppendLine(String.Empty);
                        break;
                    case "System.Int32[]":
                        var val1 = (System.Int32[])field.GetValue(this);
                        var VAL1 = val1 == null ? "(notset)" : string.Join(",", val1);

                        output.AppendLine(String.Format("# {0}", field.FieldType));
                        output.AppendLine(String.Format("{0} = {1}", field.Name, VAL1)).AppendLine(String.Empty);
                        break;
                    case "System.UInt32[]":
                        var val = (System.UInt32[])field.GetValue(this);
                        var VAL = val == null ? "(notset)" : string.Join(",", val);

                        output.AppendLine(String.Format("# {0}", field.FieldType));
                        output.AppendLine(String.Format("{0} = {1}", field.Name, VAL)).AppendLine(String.Empty);
                        break;
                    default:
                        throw new SystemException(string.Format("TYPE {0} in SETTINGS not known", FieldType)); ;
                }//swithc end

            }//foreach end

            return output.ToString();
        }









    }
}
