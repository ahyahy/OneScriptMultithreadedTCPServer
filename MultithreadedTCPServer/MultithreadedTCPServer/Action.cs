using ScriptEngine.Machine.Contexts;
using ScriptEngine.Machine;

namespace mtcps
{
    [ContextClass("МсДействие", "MsAction")]
    public class MsAction : AutoContext<MsAction>
    {
        public MsAction(IRuntimeContextInstance script, string methodName, IValue param = null)
        {
            Script = script;
            MethodName = methodName;
            Parameter = param;
        }

        [ContextProperty("ИмяМетода", "MethodName")]
        public string MethodName { get; set; }

        [ContextProperty("Параметр", "Parameter")]
        public IValue Parameter { get; set; }

        [ContextProperty("Сценарий", "Script")]
        public IRuntimeContextInstance Script { get; set; }
    }
}
