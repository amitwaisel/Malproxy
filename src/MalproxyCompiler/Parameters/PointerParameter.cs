using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MalproxyCompiler.Parameters
{
    [Flags]
    public enum ParameterDirection
    {
        in_param = 1,
        out_param = 2,
    }

    // OUT-directed argument is ALWAYS a pointer - to the stack, to user-allocated buffer or to system-allocated buffer
    // Here we assume we deal with the stack. For user/system buffers, please refer to UserBufferParameter and SystemBufferParameter.
    abstract class PointerParameter : BaseParameter
    {
        public ParameterDirection Direction { get; set; }
        public override string ParameterPrototypeTypeAndName => $"{ParameterType}* {ParameterName}";
        public override string ParameterTypeCast => $"{ParameterType}*";
        public override string ParameterTypeProtobufCast => $"{ParameterType}*";
        //public override string ParameterCode => $"&{Name}";
        public override string SetValueCode(string argname_ptr, string raw_value_name)
        {
            return $"{argname_ptr}->set_{ParameterProtobufName}_val(({ParameterTypeProtobufCast})*{raw_value_name});";
        }

        public override string GetHomeInputCode(string request_name)
        {
            if (!Direction.HasFlag(ParameterDirection.in_param))
                return base.getHomeInDeclaration(request_name); // out-only arguments will put an empty input argument as a place holder

            return base.GetHomeInputCode(request_name);
        }

        public override string GetFieldInputCode(string request_name, int index)
        {
            if (Direction.HasFlag(ParameterDirection.in_param))
                return base.GetFieldInputCode(request_name, index);

            // out-only arguments ignore the request's input arguments
            return $"{ParameterType} {ParameterName}_val = {{ 0 }}; {ParameterTypeCast} {ParameterName} = &{ParameterName}_val;"; // Just declare it on the stack, should be overridden if necessary
        }

        protected virtual string getFieldOutDeclaration(string response_name)
        {
            return $"malproxy::Argument* out_{ParameterName} = {response_name}.add_out_arguments();";
        }

        // Parse OUT parameter on attacker side (read from out_arguments)
        public virtual string GetHomeOutputCode(string result_name, int index)
        {
            if (!Direction.HasFlag(ParameterDirection.out_param))
                return string.Empty;

            return $"*{ParameterName} = ({ParameterType}){result_name}.out_arguments({index}).{ParameterProtobufName}_val();";
        }

        // Serialize OUT parameter on victim side (add to out_arguments). For in-only parameters, add a dummy out_argument
        public virtual string GetFieldOutputCode(string result_name)
        {
            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine(getFieldOutDeclaration(result_name));

            if (Direction.HasFlag(ParameterDirection.out_param))
                function_code.AppendLine($"out_{ParameterName}->set_{ParameterProtobufName}_val(*{DefaultValue});");

            return function_code.ToString();
        }
    }
}
