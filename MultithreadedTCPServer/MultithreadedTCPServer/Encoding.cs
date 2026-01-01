using ScriptEngine.HostedScript.Library.Binary;
using ScriptEngine.HostedScript.Library;
using ScriptEngine.Machine.Contexts;

namespace mtcps
{
    public class Encoding
    {
        public MsEncoding dll_obj;
        public System.Text.Encoding M_Encoding;

        public Encoding(mtcps.Encoding p1)
        {
            M_Encoding = p1.M_Encoding;
        }

        public Encoding(System.Text.Encoding p1)
        {
            M_Encoding = p1;
        }

        public Encoding()
        {
            M_Encoding = System.Text.Encoding.Default;
        }

        public mtcps.Encoding ASCII
        {
            get { return new Encoding(System.Text.Encoding.ASCII); }
        }

        public mtcps.Encoding BigEndianUnicode
        {
            get { return new Encoding(System.Text.Encoding.BigEndianUnicode); }
        }

        public string BodyName
        {
            get { return M_Encoding.BodyName; }
        }

        public mtcps.Encoding ByDefault
        {
            get { return new Encoding(System.Text.Encoding.Default); }
        }

        public string EncodingName
        {
            get { return M_Encoding.EncodingName; }
        }

        public string HeaderName
        {
            get { return M_Encoding.HeaderName; }
        }

        public mtcps.Encoding Unicode
        {
            get { return new Encoding(System.Text.Encoding.Unicode); }
        }

        public mtcps.Encoding UTF7
        {
            get { return new Encoding(System.Text.Encoding.UTF7); }
        }

        public mtcps.Encoding UTF8
        {
            get { return new Encoding(System.Text.Encoding.UTF8); }
        }

        public string WebName
        {
            get { return M_Encoding.WebName; }
        }

        public int WindowsCodePage
        {
            get { return M_Encoding.WindowsCodePage; }
        }

        public int GetByteCount(string sText)
        {
            return M_Encoding.GetByteCount(sText);
        }

        public mtcps.Encoding GetEncoding(int p1)
        {
            return new Encoding(System.Text.Encoding.GetEncoding(p1));
        }
    }

    [ContextClass("МсКодировка", "MsEncoding")]
    public class MsEncoding : AutoContext<MsEncoding>
    {
        public MsEncoding()
        {
            Encoding Encoding1 = new Encoding();
            Encoding1.dll_obj = this;
            Base_obj = Encoding1;
        }

        public MsEncoding(Encoding p1)
        {
            Encoding Encoding1 = p1;
            Encoding1.dll_obj = this;
            Base_obj = Encoding1;
        }

        public Encoding Base_obj;

        [ContextProperty("ASCII", "ASCII")]
        public MsEncoding ASCII
        {
            get { return new MsEncoding(Base_obj.ASCII); }
        }

        [ContextProperty("UTF7", "UTF7")]
        public MsEncoding UTF7
        {
            get { return new MsEncoding(Base_obj.UTF7); }
        }

        [ContextProperty("UTF8", "UTF8")]
        public MsEncoding UTF8
        {
            get { return new MsEncoding(Base_obj.UTF8); }
        }

        [ContextProperty("ИмяWeb", "WebName")]
        public string WebName
        {
            get { return Base_obj.WebName; }
        }

        [ContextProperty("ИмяЗаголовка", "HeaderName")]
        public string HeaderName
        {
            get { return Base_obj.HeaderName; }
        }

        [ContextProperty("ИмяКодировки", "EncodingName")]
        public string EncodingName
        {
            get { return Base_obj.EncodingName; }
        }

        [ContextProperty("ИмяТела", "BodyName")]
        public string BodyName
        {
            get { return Base_obj.BodyName; }
        }

        [ContextProperty("КодоваяСтраница", "WindowsCodePage")]
        public int WindowsCodePage
        {
            get { return Base_obj.WindowsCodePage; }
        }

        [ContextProperty("ОбратнаяUTF16", "BigEndianUnicode")]
        public MsEncoding BigEndianUnicode
        {
            get { return new MsEncoding(Base_obj.BigEndianUnicode); }
        }

        [ContextProperty("ПоУмолчанию", "ByDefault")]
        public MsEncoding ByDefault
        {
            get { return new MsEncoding(Base_obj.ByDefault); }
        }

        [ContextProperty("Юникод", "Unicode")]
        public MsEncoding Unicode
        {
            get { return new MsEncoding(Base_obj.Unicode); }
        }

        [ContextMethod("КоличествоБайтов", "GetByteCount")]
        public int GetByteCount(string p1)
        {
            return Base_obj.GetByteCount(p1);
        }

        [ContextMethod("ПолучитьБайты", "GetBytes")]
        public BinaryDataBuffer GetBytes(string p1)
        {
            byte[] buffer = Base_obj.M_Encoding.GetBytes(p1);
            BinaryDataBuffer bdb = new BinaryDataBuffer(new byte[0]);
            return bdb.Concat((new BinaryDataBuffer(buffer)).Read(0, buffer.Length));
        }

        [ContextMethod("ПолучитьКодировку", "GetEncoding")]
        public MsEncoding GetEncoding(int p1)
        {
            return new MsEncoding(Base_obj.GetEncoding(p1));
        }
    }
}
