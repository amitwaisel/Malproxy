using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MalproxyCompiler.Parameters
{
    // Structs are parsed as user-supplied-buffers
    abstract class StructParameter : BaseParameter
    {
        public override string ParameterProtobufName => "BufferArgument";

        public override string GetHomeInputCode(string request_name)
        {
            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine(getHomeInDeclaration(request_name));
            function_code.AppendLine($"std::unique_ptr<malproxy::BufferArgument> buffer_{ParameterName} = std::make_unique<malproxy::BufferArgument>();");
            function_code.AppendLine($"std::string buffer_{ParameterName}_data; buffer_{ParameterName}_data.assign((char*)&{ParameterName}, sizeof({ParameterName}));");
            function_code.AppendLine($"buffer_{ParameterName}->set_data(buffer_{ParameterName}_data);");
            function_code.AppendLine($"buffer_{ParameterName}->set_size(buffer_{ParameterName}_data.size());");
            function_code.AppendLine($"buffer_{ParameterName}->set_type(malproxy::BufferType::BufferType_UserAllocated);");
            function_code.AppendLine($"arg_{ParameterName}->set_allocated_buffer_val(buffer_{ParameterName}.release());");
            return function_code.ToString();
        }

        public override string GetFieldInputCode(string request_name, int index)
        {
            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine($"auto {ParameterName}_buffer = {request_name}.in_arguments({index}).{ParameterProtobufName}_val();");
            function_code.AppendLine($"auto {ParameterName}_buffer_data = {ParameterName}_buffer.data();");
            function_code.AppendLine($"{ParameterType}* {ParameterName}_ptr = ({ParameterType}*){ParameterName}_buffer_data.data();");
            function_code.AppendLine($"{ParameterType} {ParameterName} = *{ParameterName}_ptr;"); // Support passing nullptr if buffer is empty
            return function_code.ToString();
        }
    }

    // Structs are parsed as user-supplied-buffers
    abstract class StructPtrParameter : PointerParameter
    {
        public virtual string StructType { get; set; }
        protected override string ParameterType => StructType;
        public override string ParameterProtobufName => "buffer";
        public override string SetValueCode(string argname_ptr, string raw_value_name)
        {;
            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine($"std::unique_ptr<malproxy::BufferArgument> {argname_ptr}_value_buffer = std::make_unique<malproxy::BufferArgument>();");
            function_code.AppendLine($"std::string {argname_ptr}_value_buffer_data; {argname_ptr}_value_buffer_data.assign((char*){raw_value_name}, sizeof(*{raw_value_name}));");
            function_code.AppendLine($"{argname_ptr}_value_buffer->set_data({argname_ptr}_value_buffer_data);");
            function_code.AppendLine($"{argname_ptr}_value_buffer->set_size({argname_ptr}_value_buffer_data.size());");
            function_code.AppendLine($"{argname_ptr}_value_buffer->set_type(malproxy::BufferType::BufferType_UserAllocated);");
            function_code.AppendLine($"{argname_ptr}->set_allocated_{ParameterProtobufName}_val({argname_ptr}_value_buffer.release());");
            return function_code.ToString();
        }

        public override string GetReturnValueCode(string argname)
        {
            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine($"malproxy::BufferArgument* {argname}_buffer_leaked_pointer = {argname}.mutable_return_value()->release_buffer_val();");
            function_code.AppendLine($"return ({ParameterTypeCast}){argname}_buffer_leaked_pointer->data().data();");
            return function_code.ToString();
        }

        public override string GetHomeInputCode(string request_name)
        {
            if (!Direction.HasFlag(ParameterDirection.in_param))
                return base.GetHomeInputCode(request_name);

            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine(getHomeInDeclaration(request_name));
            function_code.AppendLine(SetValueCode($"arg_{ParameterName}", ParameterName));
            return function_code.ToString();
        }

        public override string GetFieldInputCode(string request_name, int index)
        {
            if (!Direction.HasFlag(ParameterDirection.in_param))
                return base.GetFieldInputCode(request_name, index);

            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine($"auto {ParameterName}_buffer = {request_name}.in_arguments({index}).{ParameterProtobufName}_val();");
            function_code.AppendLine($"auto {ParameterName}_buffer_data = {ParameterName}_buffer.data();");
            function_code.AppendLine($"{ParameterTypeCast} {ParameterName} = ({ParameterTypeCast}){ParameterName}_buffer_data.data();");
            return function_code.ToString();
        }
        public override string GetFieldOutputCode(string result_name)
        {
            if (!Direction.HasFlag(ParameterDirection.out_param))
                return base.GetFieldOutputCode(result_name);

            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine(getFieldOutDeclaration(result_name));
            function_code.AppendLine($"std::unique_ptr<malproxy::BufferArgument> {ParameterName}_buffer_ptr = std::make_unique<malproxy::BufferArgument>();");
            function_code.AppendLine($"std::string out_{ParameterName}_data; out_{ParameterName}_data.assign((char*){ParameterName}, sizeof(*{ParameterName}));");
            function_code.AppendLine($"{ParameterName}_buffer_ptr->set_data(out_{ParameterName}_data);");
            function_code.AppendLine($"{ParameterName}_buffer_ptr->set_size(out_{ParameterName}_data.size());");
            function_code.AppendLine($"{ParameterName}_buffer_ptr->set_type(malproxy::BufferType::BufferType_UserAllocated);");
            function_code.AppendLine($"out_{ParameterName}->set_allocated_{ParameterProtobufName}_val({ParameterName}_buffer_ptr.release());");
            return function_code.ToString();
        }

        public override string GetHomeOutputCode(string result_name, int result_out_param_index)
        {
            if (!Direction.HasFlag(ParameterDirection.out_param))
                return base.GetHomeOutputCode(result_name, result_out_param_index);

            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine($"auto out_buffer_{ParameterName} = {result_name}.out_arguments({result_out_param_index}).{ParameterProtobufName}_val();");
            function_code.AppendLine($"auto out_buffer_{ParameterName}_data = out_buffer_{ParameterName}.data();");
            function_code.AppendLine($"if (out_buffer_{ParameterName}.type() == malproxy::BufferType::BufferType_UserAllocated && !out_buffer_{ParameterName}_data.empty())");
            function_code.AppendLine($" memcpy({ParameterName}, out_buffer_{ParameterName}_data.data(), std::min(out_buffer_{ParameterName}_data.size(), sizeof(*{ParameterName})));");
            return function_code.ToString();
        }
    }

    class PUNICODE_STRING : StructPtrParameter
    {
        public override string StructType => "UNICODE_STRING";
    }

    class PSLIST_HEADER : StructPtrParameter
    {
        public override string StructType => "SLIST_HEADER";
    }
    class PSLIST_ENTRY : StructPtrParameter
    {
        public override string StructType => "SLIST_ENTRY";
    }
    class PCONTEXT : StructPtrParameter
    {
        public override string StructType => "CONTEXT";
    }
    class PUNWIND_HISTORY_TABLE : StructPtrParameter
    {
        public override string StructType => "UNWIND_HISTORY_TABLE";
    }
    class PRUNTIME_FUNCTION : StructPtrParameter
    {
        public override string StructType => "RUNTIME_FUNCTION";
    }
    class PKNONVOLATILE_CONTEXT_POINTERS : StructPtrParameter
    {
        public override string StructType => "KNONVOLATILE_CONTEXT_POINTERS";
    }
    class PEXCEPTION_POINTERS : StructPtrParameter
    {
        public override string StructType => "struct _EXCEPTION_POINTERS";
    }
    class LPSTARTUPINFOW : StructPtrParameter
    {
        public override string StructType => "STARTUPINFOW";
    }
    class LPCRITICAL_SECTION : StructPtrParameter
    {
        public override string StructType => "RTL_CRITICAL_SECTION";
    }
}
