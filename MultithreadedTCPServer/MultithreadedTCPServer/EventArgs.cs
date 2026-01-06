using ScriptEngine.Machine.Contexts;
using ScriptEngine.Machine;
using ScriptEngine.HostedScript.Library.Binary;

namespace mtcps
{
    [ContextClass("МсАргументыСобытия", "MsEventArgs")]
    public class MsEventArgs : AutoContext<MsEventArgs>
    {
        public MsEventArgs()
        {
        }

        public MsEventArgs(BinaryDataBuffer p1)
        {
            data = p1;
        }
        public MsEventArgs(MsEventArgs p1)
        {
            data = p1.Data;
            clientId = p1.ClientId;
            serverError = p1.ServerError;
            parameter = p1.Parameter;
        }

        public IValue parameter;
        [ContextProperty("Параметр", "Parameter")]
        public IValue Parameter
        {
            get { return parameter; }
        }

        public string serverError = null;
        [ContextProperty("ОшибкаСервера", "ServerError")]
        public string ServerError
        {
            get { return serverError; }
        }

        public BinaryDataBuffer data = null;
        [ContextProperty("Данные", "Data")]
        public BinaryDataBuffer Data
        {
            get { return data; }
        }

        public string clientId = null;
        [ContextProperty("ИдентификаторКлиента", "ClientId")]
        public string ClientId
        {
            get { return clientId; }
        }

        public MsAction eventAction;
        public MsAction EventAction
        {
            get { return eventAction; }
        }
    }
}
