using ScriptEngine.HostedScript.Library.Binary;
using ScriptEngine.Machine.Contexts;
using System.Threading.Tasks;

namespace mtcps
{
    [ContextClass("МсПотокСети", "MsNetworkStream")]
    public class MsNetworkStream : AutoContext<MsNetworkStream>
    {
        public MsNetworkStream(System.Net.Sockets.NetworkStream p1)
        {
            Base_obj = p1;
        }

        public System.Net.Sockets.NetworkStream Base_obj;

        [ContextProperty("ВозможностьЗаписи", "CanWrite")]
        public bool CanWrite
        {
            get { return Base_obj.CanWrite; }
        }

        [ContextProperty("ВозможностьЧтения", "CanRead")]
        public bool CanRead
        {
            get { return Base_obj.CanRead; }
        }

        [ContextProperty("ДанныеДоступны", "DataAvailable")]
        public bool DataAvailable
        {
            get { return Base_obj.DataAvailable; }
        }

        [ContextMethod("Закрыть", "Close")]
        public void Close()
        {
            Base_obj.Close();
        }

        [ContextMethod("Записать", "Write")]
        public void Write(BinaryDataBuffer p1, int p2, int p3)
        {
            Base_obj.Write(p1.Bytes, 0, p3);
        }

        [ContextMethod("Прочитать", "Read")]
        public BinaryDataBuffer Read(int p1, int p2)
        {
            byte[] buffer = new byte[p2];
            Base_obj.Read(buffer, p1, p2);
            BinaryDataBuffer bdb = new BinaryDataBuffer(new byte[0]);
            return bdb.Concat((new BinaryDataBuffer(buffer)).Read(0, buffer.Length));
        }

        [ContextMethod("ПрочитатьВБуферДвоичныхДанных", "ReadToBinaryDataBuffer")]
        public BinaryDataBuffer ReadToBinaryDataBuffer()
        {
            Utils.GlobalContext().Echo("ReadToBinaryDataBufferReadToBinaryDataBuffer");

            BinaryDataBuffer bdb = ReadToBDB().Result;
            return bdb;
        }

        public Task<BinaryDataBuffer> ReadToBDB()
        {
            return ReadToBuffer();
        }

        public async Task<BinaryDataBuffer> ReadToBuffer()
        {
            BinaryDataBuffer bdb = new BinaryDataBuffer(new byte[0]);
            byte[] Buffer = new byte[1024];
            while (true)
            {
                int bytes = await this.Base_obj.ReadAsync(Buffer, 0, Buffer.Length);
                if (bytes > 0)
                {
                    bdb = bdb.Concat((new BinaryDataBuffer(Buffer)).Read(0, bytes));
                    return bdb;
                }
                return bdb;
            }
        }

        [ContextMethod("ЧитатьБайт", "ReadByte")]
        public int ReadByte()
        {
            return Base_obj.ReadByte();
        }
    }
}
