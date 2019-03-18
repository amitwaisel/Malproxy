using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MalproxyCompiler.Parameters
{
    class StringParameter : PointerParameter
    {
        protected override string ParameterType => "char";
        public override string ParameterProtobufName => "string";

        public override string GetHomeInputCode(string request_name)
        {
            // GetHomeInputCode same as base class, char* will be converted to std::string automatically
            return base.GetHomeInputCode(request_name);
            //            return $@"{getHomeInDeclaration(request_name)}
            //arg_{Name}->set_{ProtobufName}_val({Name});";
        }
        public override string GetFieldInputCode(string request_name, int index)
        {
            if (!Direction.HasFlag(ParameterDirection.in_param))
                return base.GetFieldInputCode(request_name, index);

            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine($"auto {ParameterName}_string = {request_name}.in_arguments({index}).{ParameterProtobufName}_val();");
            function_code.AppendLine($"{ParameterType}* {ParameterName} = ({ParameterTypeCast}){ParameterName}_string.c_str();");
            return function_code.ToString();
        }

        public override string GetFieldOutputCode(string result_name)
        {
            // GetFieldOutputCode same as base class, char* will be converted to std::string automatically
            return base.GetFieldOutputCode(result_name);
        }

        public override string GetHomeOutputCode(string result_name, int index)
        {
            if (!Direction.HasFlag(ParameterDirection.out_param))
                return base.GetHomeOutputCode(result_name, index);

            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine($"auto {ParameterName}_out_string = {result_name}.out_arguments({index}).{ParameterProtobufName}_val();");
            function_code.AppendLine($"strncpy({ParameterName}, {ParameterName}_out_string.c_str(), {ParameterName}_out_string.size());");
            return function_code.ToString();
        }
    }

    class WstringParameter : PointerParameter
    {
        protected override string ParameterType => "wchar_t";
        public override string ParameterProtobufName => "wstring";

        public override string GetHomeInputCode(string request_name)
        {
            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine($"{getHomeInDeclaration(request_name)}");
            function_code.AppendLine($"arg_{ParameterName}->set_{ParameterProtobufName}_val(StringUtils::Utf16ToUtf8({ParameterName}));");
            return function_code.ToString();
        }

        public override string GetFieldInputCode(string request_name, int index)
        {
            if (!Direction.HasFlag(ParameterDirection.in_param))
                return base.GetFieldInputCode(request_name, index);

            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine($"auto {ParameterName}_data = {request_name}.in_arguments({index}).{ParameterProtobufName}_val();");
            function_code.AppendLine($"auto {ParameterName}_wstring = StringUtils::Utf8ToUtf16({ParameterName}_data);");
            function_code.AppendLine($"{ParameterTypeCast} {ParameterName} = ({ParameterTypeCast}){ParameterName}_wstring.c_str();");
            return function_code.ToString();
        }

        public override string GetFieldOutputCode(string result_name)
        {
            if (!Direction.HasFlag(ParameterDirection.out_param))
                return base.GetFieldOutputCode(result_name);

            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine(getFieldOutDeclaration(result_name));
            function_code.AppendLine($"std::string ascii_{ParameterName} = StringUtils::Utf16ToUtf8({ParameterName});");
            function_code.AppendLine($"out_{ParameterName}->set_{ParameterProtobufName}_val(ascii_{ParameterName});");
            return function_code.ToString();
        }

        public override string GetHomeOutputCode(string result_name, int index)
        {
            if (!Direction.HasFlag(ParameterDirection.out_param))
                return base.GetHomeOutputCode(result_name, index);

            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine($"auto {ParameterName}_out_string = {result_name}.out_arguments({index}).{ParameterProtobufName}_val();");
            function_code.AppendLine($"std::wstring {ParameterName}_out_wstring = StringUtils::Utf8ToUtf16({ParameterName}_out_string);");
            function_code.AppendLine($"wcsncpy({ParameterName}, {ParameterName}_out_wstring.c_str(), {ParameterName}_out_wstring.size());");
            return function_code.ToString();
        }
    }
}
