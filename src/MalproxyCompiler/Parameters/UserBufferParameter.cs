using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MalproxyCompiler.Parameters
{
    // User-allocated buffer (Name) and its size (SizeName). SizeName is in-only, passed by-value.
    // For pointer-in-out-SizeName parameter, use UserBufferPtrParameter
    class UserBufferParameter : PointerParameter
    {
        public string BufferSizeName { get; set; }
        public virtual string BufferSizeType => "DWORD";

        protected override string ParameterType => "LPVOID";
        public override string ParameterProtobufName => "buffer";

        public virtual string GetBufferSize => BufferSizeName;

        public override string ParameterTypeCast => $"{ParameterType}*,{BufferSizeType}";
        public override string ParameterPrototypeTypeAndName => $"{ParameterType}* {ParameterName}, {BufferSizeType} {BufferSizeName}";
        public override string ParameterPrototypeName => $"{ParameterName},{BufferSizeName}";
        public override string ParameterInFunctionCall => $"&{ParameterName},{BufferSizeName}";

        public override string SetValueCode(string argname_ptr, string raw_value_name)
        {
            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine($"std::unique_ptr<malproxy::BufferArgument> {argname_ptr}_value_buffer = std::make_unique<malproxy::BufferArgument>();");
            function_code.AppendLine($"std::string {argname_ptr}_value_buffer_data; {argname_ptr}_value_buffer_data.assign((char*){raw_value_name}, {GetBufferSize});");
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
            StringBuilder function_code = new StringBuilder();
            if (!Direction.HasFlag(ParameterDirection.in_param))
            {
                function_code.AppendLine($"{ParameterType} {ParameterName} = nullptr;");
                function_code.AppendLine($"{BufferSizeType} {BufferSizeName} = 0;");
            }
            else
            {
                function_code.AppendLine($"auto {ParameterName}_buffer = {request_name}.in_arguments({index}).{ParameterProtobufName}_val();");
                function_code.AppendLine($"auto {ParameterName}_buffer_data = {ParameterName}_buffer.data();");
                function_code.AppendLine(
                    $"{ParameterType} {ParameterName} = ({ParameterName}_buffer_data.size() > 0) ? ({ParameterType}){ParameterName}_buffer_data.data() : nullptr;"); // Support passing nullptr if buffer is empty
                function_code.AppendLine($"{BufferSizeType} {BufferSizeName} = ({BufferSizeType}){ParameterName}_buffer.size();");
            }
            return function_code.ToString();
        }

        public override string GetFieldOutputCode(string result_name)
        {
            if (!Direction.HasFlag(ParameterDirection.out_param))
                return base.GetFieldOutputCode(result_name);

            // For void* buff with DWORD buff_size:
            // malproxy::Argument* out_buff = result.add_out_arguments();
            // std::unique_ptr<malproxy::BufferType> buff_buffer_ptr = std::make_unique<malproxy::BufferType>();
            // std::string out_buff_data; out_buff_data.assign(buff, buff_size);
            // buff_buffer_ptr->set_data(out_buff_data);
            // buff_buffer_ptr->set_size(buff_size);
            // buff_buffer_ptr->set_type(malproxy::BufferType::UserAllocated);
            // out_buff->set_allocated_buffer_val(buff_buffer_ptr.release());

            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine(getFieldOutDeclaration(result_name));
            function_code.AppendLine($"std::unique_ptr<malproxy::BufferArgument> {ParameterName}_buffer_ptr = std::make_unique<malproxy::BufferArgument>();");
            function_code.AppendLine($"std::string out_{ParameterName}_data; out_{ParameterName}_data.assign((char*){ParameterName}, {GetBufferSize});");
            function_code.AppendLine($"{ParameterName}_buffer_ptr->set_data(out_{ParameterName}_data);");
            function_code.AppendLine($"{ParameterName}_buffer_ptr->set_size({GetBufferSize});"); // Ignored when BufferSizeType is not a pointer
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
            function_code.AppendLine($" memcpy({ParameterName}, out_buffer_{ParameterName}_data.data(), std::min(({BufferSizeType})out_buffer_{ParameterName}_data.size(), {GetBufferSize}));");
            return function_code.ToString();
        }
    }
    
    // Same as UserBufferParameter with BufferSize as in/out pointer
    class UserBufferPtrParameter : UserBufferParameter
    {
        public override string GetBufferSize => $"*{BufferSizeName}";
        public override string BufferSizeType => "DWORD*";
        public override string ParameterPrototypeName => $"&{ParameterName},&{BufferSizeName}";

        public override string GetFieldInputCode(string request_name, int index)
        {
            StringBuilder function_code = new StringBuilder();
            if (!Direction.HasFlag(ParameterDirection.in_param))
            {
                function_code.AppendLine($"{ParameterType} {ParameterName} = nullptr;");
                function_code.AppendLine($"SWORD {BufferSizeName}_val = 0; {BufferSizeType} {BufferSizeName} = &{BufferSizeName}_val;");
            }
            else
            {
                function_code.AppendLine($"auto {ParameterName}_buffer = {request_name}.in_arguments({index}).{ParameterProtobufName}_val();");
                function_code.AppendLine($"auto {ParameterName}_buffer_data = {ParameterName}_buffer.data();");
                function_code.AppendLine(
                    $"{ParameterType} {ParameterName} = ({ParameterName}_buffer_data.size() > 0) ? {ParameterName}_buffer_data.data() : nullptr;"); // Support passing nullptr if buffer is empty
                function_code.AppendLine($"auto {BufferSizeName}_value = {ParameterName}_buffer.size();");
                function_code.AppendLine($"{BufferSizeType} {BufferSizeName} = &{BufferSizeName}_value;");
            }

            return function_code.ToString();
        }

        public override string GetFieldOutputCode(string result_name)
        {
            // Same as base.GetFieldOutputCode because we overridden GetBufferSize property
            return base.GetFieldOutputCode(result_name);
        }

        public override string GetHomeOutputCode(string result_name, int result_out_param_index)
        {
            if (!Direction.HasFlag(ParameterDirection.out_param))
                return base.GetHomeOutputCode(result_name, result_out_param_index);

            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine(base.GetHomeOutputCode(result_name, result_out_param_index));
            function_code.AppendLine($"{GetBufferSize} = out_buffer_{ParameterName}.size();");
            return function_code.ToString();
        }
    }
}
