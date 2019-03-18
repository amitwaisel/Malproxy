using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MalproxyCompiler.Parameters
{

    class LargeIntegerPtrParameter : PointerParameter
    {
        protected override string ParameterType => "LARGE_INTEGER";
        public override string ParameterProtobufName => "uint64";

        public override string GetHomeInputCode(string request_name)
        {
            return $@"{getHomeInDeclaration(request_name)}
arg_{ParameterName}->set_{ParameterProtobufName}_val({ParameterName}->QuadPart);";
        }

        public override string GetFieldInputCode(string request_name, int index)
        {
            StringBuilder function_code = new StringBuilder();

            function_code.AppendLine($"LARGE_INTEGER {ParameterName}_val = {{0}};");
            function_code.AppendLine($"{ParameterTypeCast} {ParameterName} = &{ParameterName}_val;");

            if (!Direction.HasFlag(ParameterDirection.in_param))
                function_code.AppendLine($"{ParameterName}->QuadPart = {request_name}.in_arguments({index}).{ParameterProtobufName}_val();");

            return function_code.ToString();
        }

        public override string GetFieldOutputCode(string result_name)
        {
            if (!Direction.HasFlag(ParameterDirection.out_param))
                return string.Empty;

            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine(getFieldOutDeclaration(result_name));
            function_code.AppendLine($"out_{ParameterName}->set_{ParameterProtobufName}_val({ParameterName}->QuadPart);");
            return function_code.ToString();
        }

        public override string GetHomeOutputCode(string result_name, int index)
        {
            if (!Direction.HasFlag(ParameterDirection.out_param))
                return string.Empty;

            return $"{ParameterName}->QuadPart = {result_name}.out_arguments({index}).{ParameterProtobufName}_val();";
        }
    }

    class FileTimePtrParameter : PointerParameter
    {
        protected override string ParameterType => "FILETIME";
        public override string ParameterProtobufName => "uint64";

        public override string GetHomeInputCode(string request_name)
        {
            return $@"{getHomeInDeclaration(request_name)}
arg_{ParameterName}->set_{ParameterProtobufName}_val(((LARGE_INTEGER*){ParameterName})->QuadPart);";
        }

        public override string GetFieldInputCode(string request_name, int index)
        {
            StringBuilder function_code = new StringBuilder();

            function_code.AppendLine($"{ParameterType} {ParameterName}_val = {{0}};");
            function_code.AppendLine($"{ParameterTypeCast} {ParameterName} = &{ParameterName}_val;");

            if (!Direction.HasFlag(ParameterDirection.in_param))
                function_code.AppendLine($"((LARGE_INTEGER*){ParameterName})->QuadPart = {request_name}.in_arguments({index}).{ParameterProtobufName}_val();");

            return function_code.ToString();
        }

        public override string GetFieldOutputCode(string result_name)
        {
            if (!Direction.HasFlag(ParameterDirection.out_param))
                return string.Empty;

            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine(getFieldOutDeclaration(result_name));
            function_code.AppendLine($"out_{ParameterName}->set_{ParameterProtobufName}_val(((LARGE_INTEGER*){ParameterName})->QuadPart);");
            return function_code.ToString();
        }

        public override string GetHomeOutputCode(string result_name, int index)
        {
            if (!Direction.HasFlag(ParameterDirection.out_param))
                return string.Empty;

            return $"((LARGE_INTEGER*){ParameterName})->QuadPart = {result_name}.out_arguments({index}).{ParameterProtobufName}_val();";
        }
    }

    class HandleParameter : BaseParameter
    {
        protected override string ParameterType => "HANDLE";
        public override string ParameterProtobufName => "HandleType";

        public override string GetValueCode(string argname)
        {
            // (HANDLE){response}.return_value().handle_val().handle();
            return $"({ParameterType}){argname}.handle_val().handle()";
        }
        public override string GetReturnValueCode(string argname)
        {
            return $"return ({ParameterTypeCast}){argname}.return_value().handle_val().handle();";
        }

        public override string SetValueCode(string argname_ptr, string raw_value_name)
        {
            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine($"std::unique_ptr<malproxy::{ParameterProtobufName}> handle_{argname_ptr}_{ParameterName} = std::make_unique<malproxy::{ParameterProtobufName}>();");
            function_code.AppendLine($"handle_{argname_ptr}_{ParameterName}->set_handle((uint64_t){raw_value_name});");
            function_code.AppendLine($"{argname_ptr}->set_allocated_handle_val(handle_{argname_ptr}_{ParameterName}.release());");
            return function_code.ToString();
        }


        public override string GetHomeInputCode(string request_name)
        {
            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine(getHomeInDeclaration(request_name));
            function_code.AppendLine($"std::unique_ptr<malproxy::{ParameterProtobufName}> handle_{ParameterName} = std::make_unique<malproxy::{ParameterProtobufName}>();");
            function_code.AppendLine($"handle_{ParameterName}->set_handle((uint64_t){ParameterName});");
            function_code.AppendLine($"arg_{ParameterName}->set_allocated_handle_val(handle_{ParameterName}.release());");
            return function_code.ToString();
        }

        public override string GetFieldInputCode(string request_name, int index)
        {
            return $"{ParameterType} {ParameterName} = ({ParameterType}){request_name}.in_arguments({index}).handle_val().handle();";
        }
    }

    class HandlePtrParameter : PointerParameter
    {
        protected override string ParameterType => "HANDLE";
        public override string ParameterProtobufName => "HandleType";

        public override string GetHomeInputCode(string request_name)
        {
            if (!Direction.HasFlag(ParameterDirection.in_param))
                return base.getHomeInDeclaration(request_name);

            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine(getHomeInDeclaration(request_name));
            function_code.AppendLine($"std::unique_ptr<malproxy::{ParameterProtobufName}> handle_{ParameterName} = std::make_unique<malproxy::{ParameterProtobufName}>();");
            function_code.AppendLine($"handle_{ParameterName}->set_handle((uint64_t)*{ParameterName});");
            function_code.AppendLine($"arg_{ParameterName}->set_allocated_handle_val(handle_{ParameterName}.release());");
            return function_code.ToString();
        }

        // The API expects HANDLE* as parameter, we need to parse the HANDLE and pass a pointer to it
        public override string GetFieldInputCode(string request_name, int index)
        {
            string input_value = (Direction.HasFlag(ParameterDirection.in_param)) ? $"(HANDLE){request_name}.in_arguments({index}).handle_val().handle()" : "nullptr";

            return $@"HANDLE {ParameterName}_handle = {input_value};
{ParameterTypeCast} {ParameterName} = ({ParameterTypeCast})&{ParameterName}_handle;";
        }

        public override string GetHomeOutputCode(string result_name, int result_out_param_index)
        {
            if (!Direction.HasFlag(ParameterDirection.out_param))
                return base.GetHomeOutputCode(result_name, result_out_param_index);

            return $"*{ParameterName} = ({ParameterType}){result_name}.out_arguments({result_out_param_index}).handle_val().handle();";
        }

        public override string GetFieldOutputCode(string result_name)
        {
            if (!Direction.HasFlag(ParameterDirection.out_param))
                return base.GetFieldOutputCode(result_name);

            StringBuilder function_code = new StringBuilder();
            function_code.AppendLine(getFieldOutDeclaration(result_name));
            function_code.AppendLine($"std::unique_ptr<malproxy::{ParameterProtobufName}> {ParameterName}_handle_ptr = std::make_unique<malproxy::{ParameterProtobufName}>();");
            function_code.AppendLine($"{ParameterName}_handle_ptr->set_handle(*{ParameterName})");
            function_code.AppendLine($"out_{ParameterName}->set_allocated_handle_val({ParameterName}_handle_ptr.release());");
            return function_code.ToString();
        }
    }
}
