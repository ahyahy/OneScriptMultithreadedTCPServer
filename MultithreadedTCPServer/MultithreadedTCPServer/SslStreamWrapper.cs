using ScriptEngine.HostedScript.Library.Binary;
using ScriptEngine.Machine.Contexts;
using System.Threading.Tasks;
using System.Net.Security;

namespace mtcps
{
    [ContextClass("МсSSLПоток", "MsSslStreamWrapper")]
    public class MsSslStreamWrapper : AutoContext<MsSslStreamWrapper>
    {
        private SslStream _sslStream;

        public MsSslStreamWrapper(SslStream sslStream)
        {
            _sslStream = sslStream;
        }

        [ContextProperty("МожноЧитать", "CanRead")]
        public bool CanRead => _sslStream?.CanRead ?? false;

        [ContextProperty("МожноПисать", "CanWrite")]
        public bool CanWrite => _sslStream?.CanWrite ?? false;

        [ContextMethod("ПрочитатьВБуферДвоичныхДанных", "ReadToBinaryDataBuffer")]
        public BinaryDataBuffer ReadToBinaryDataBuffer()
        {
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
                int bytes = await this._sslStream.ReadAsync(Buffer, 0, Buffer.Length);
                if (bytes > 0)
                {
                    bdb = bdb.Concat((new BinaryDataBuffer(Buffer)).Read(0, bytes));
                    return bdb;
                }
                return bdb;
            }
        }
    }
}
