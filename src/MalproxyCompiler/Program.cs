using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MalproxyCompiler.Parameters;

namespace MalproxyCompiler
{

    class FunctionCodeGenerator
    {
        private BaseParameter _return_type;
        private string _dll_name;
        private string _function_name;
        private List<BaseParameter> _parameters = new List<BaseParameter>();

        public string DllName => _dll_name;
        public string FunctionName => _function_name;

        public FunctionCodeGenerator(string dll_name, string function_name, BaseParameter return_type)
        {
            _dll_name = dll_name.ToLower();
            _function_name = function_name;
            _return_type = return_type;
        }

        public void AddParameter(BaseParameter parameter)
        {
            _parameters.Add(parameter);
        }

        public string GenerateFieldCode()
        {
            StringBuilder function_code = new StringBuilder();
            string request = "request";
            string result = "result";
            string real_func = "real_func";
            function_code.AppendLine($@"malproxy::CallFuncResponse {_function_name}_stub(const malproxy::CallFuncRequest& {request}, FARPROC {real_func})");
            function_code.AppendLine("{");
            function_code.AppendLine($"printf(\"Running function %s ! %s\\n\", {request}.dll_name().c_str(), {request}.function_name().c_str());");

            // out-only arguments will put an empty input argument as a place holder
            for (int argument_index = 0; argument_index < _parameters.Count; argument_index++)
            {
                function_code.AppendLine(_parameters[argument_index].GetFieldInputCode(request, argument_index));
            }

            string return_type = _return_type != null ? _return_type.ParameterTypeCast : "void";
            string parameter_types = string.Join(",", _parameters.Select(p => p.ParameterTypeCast));
            string parameter_names = string.Join(",", _parameters.Select(p => p.ParameterInFunctionCall));

            // HANDLE raw_result = ((HANDLE(*)(LPCWSTR, DWORD, DWORD, LPVOID, DWORD, DWORD, HANDLE))real_func)(lpFileName, dwDesiredAccess, dwShareMode, lpSecurityAttributes, dwCreationDisposition, dwFlagsAndAttributes, hTemplateFile);
            string raw_retval = "raw_retval";
            string retval = "retval";
            string return_value_code = "";
            if (_return_type != null)
                return_value_code = $"{return_type} {raw_retval} = ";
            function_code.AppendLine($"{return_value_code} (({return_type}(*)({parameter_types})){real_func})({parameter_names});");

            function_code.AppendLine($"malproxy::CallFuncResponse {result};");
            if (_return_type != null)
            {
                function_code.AppendLine($"malproxy::Argument {retval}_value; malproxy::Argument* {retval} = &{retval}_value;");
                function_code.AppendLine(_return_type.SetValueCode(retval, raw_retval));
                function_code.AppendLine($"std::unique_ptr<malproxy::Argument> {retval}_allocated_ptr = std::make_unique<malproxy::Argument>({retval}_value);");
                function_code.AppendLine($"{result}.set_allocated_return_value({retval}_allocated_ptr.release());");
            }

            foreach (var arg in _parameters)
            {
                if (!(arg is PointerParameter))
                    continue;
                var arg_ptr = arg as PointerParameter;
                if (!arg_ptr.Direction.HasFlag(ParameterDirection.out_param))
                    continue;

                function_code.AppendLine(arg_ptr.GetFieldOutputCode(result));
            }

            function_code.AppendLine($"return {result};");
            function_code.AppendLine("}");

            return function_code.ToString();
        }

        public string GenerateHomeProxyCode()
        {
            StringBuilder function_code = new StringBuilder();
            string return_type = (_return_type != null) ? _return_type.ParameterTypeCast : "void";
            function_code.AppendLine($@"{return_type} WINAPI Malproxy_{_function_name} (");
            function_code.AppendLine("    MalproxySession* client");
            foreach (var param in _parameters)
                function_code.AppendLine($"    ,{param.ParameterPrototypeTypeAndName}");
            function_code.AppendLine(") {");

            foreach (var null_param in _parameters.Where(arg => arg is NullParameter))
                function_code.AppendLine($"if ({null_param.ParameterName} != nullptr) THROW(\"{null_param.ParameterName} must be nullptr\");");

            string request = "request";
            function_code.AppendLine($"malproxy::CallFuncRequest {request};");
            function_code.AppendLine($"{request}.set_dll_name(\"{_dll_name}\");");
            function_code.AppendLine($"{request}.set_function_name(\"{_function_name}\");");

            function_code.AppendLine();

            foreach (var param in _parameters)
                function_code.AppendLine(param.GetHomeInputCode(request));

            function_code.AppendLine();

            string response = "response";
            function_code.AppendLine($"auto {response} = client->CallFunc({request});");

            int out_arg_index = 0;
            foreach (var param in _parameters)
            {
                if (!(param is PointerParameter))
                    continue;
                var directional_param = param as PointerParameter;
                if (!directional_param.Direction.HasFlag(ParameterDirection.out_param))
                    continue;

                function_code.AppendLine(directional_param.GetHomeOutputCode(response, out_arg_index));

                out_arg_index++;
            }

            if (_return_type != null)
            {
                function_code.AppendLine(_return_type.GetReturnValueCode(response));
            }

            function_code.AppendLine("}");
            return function_code.ToString();
        }

        // The stub has the same signature as the original WinAPI function. It calls the Malproxy_Xxx proxy function, using the global RPC session object.
        public string GenerateHomeStubCode()
        {
            StringBuilder function_code = new StringBuilder();
            string return_type = (_return_type != null) ? _return_type.ParameterTypeCast : "void";
            function_code.AppendLine($@"{return_type} WINAPI {_function_name}_stub (");
            function_code.AppendLine(string.Join(",", _parameters.Select(param => $"{param.ParameterPrototypeTypeAndName}")));
            function_code.AppendLine(") {");
            function_code.AppendLine($"return Malproxy_{_function_name}(MalproxyClientRunner::Instance().Session().get()");
            foreach (var param in _parameters)
                function_code.AppendLine($"    ,{param.ParameterPrototypeName}");
            function_code.AppendLine(");");
            function_code.AppendLine("}");
            return function_code.ToString();
        }
    }

    class CodeGenerator
    {
        private string GenerateHomeCode(IEnumerable<FunctionCodeGenerator> functions)
        {
            StringBuilder code_builder = new StringBuilder();

            code_builder.AppendLine($"#include \"RpcLib/MalproxySession.h\"");
            code_builder.AppendLine($"#include \"Framework/Utils.h\"");
            code_builder.AppendLine($"#include \"Framework/NtHelper.h\"");
            code_builder.AppendLine($"#include <Windows.h>");
            code_builder.AppendLine($"#include \"MalproxyClientRunner.h\"");
            code_builder.AppendLine();

            foreach (var func in functions)
            {
                code_builder.AppendLine(func.GenerateHomeProxyCode());
                code_builder.AppendLine(func.GenerateHomeStubCode());
            }

            var hooks = functions.GroupBy(func => func.DllName).Select(gr => new { module = gr.Key, funcs = gr.Select(f => f.FunctionName)});

            code_builder.AppendLine("std::map<std::string, std::map<std::string, FARPROC>> autogenerated_stubs = {");
            foreach (var module in hooks)
            {
                code_builder.AppendLine("{");
                code_builder.AppendLine($"\"{module.module}\",");
                code_builder.AppendLine("{");
                foreach (var func in module.funcs)
                    code_builder.AppendLine($"{{ \"{func}\", (FARPROC){func}_stub }},");
                code_builder.AppendLine("}");
                code_builder.AppendLine("},");
            }
            code_builder.AppendLine("};");
            return code_builder.ToString();
        }

        public void GenerateHomeCode(string output_path, IEnumerable<FunctionCodeGenerator> functions)
        {
            string code = GenerateHomeCode(functions);
            System.IO.File.WriteAllText(output_path, code);
        }

        private string GenerateFieldCode(IEnumerable<FunctionCodeGenerator> functions)
        {
            StringBuilder code_builder = new StringBuilder();
            code_builder.AppendLine($"#include \"RpcLib/MalproxyServer.h\"");
            code_builder.AppendLine($"#include \"Framework/Utils.h\"");
            code_builder.AppendLine($"#include \"Framework/NtHelper.h\"");

            foreach (var func in functions)
            {
                code_builder.AppendLine(func.GenerateFieldCode());
            }

            var hooks = functions.GroupBy(func => func.DllName).Select(gr => new { module = gr.Key, funcs = gr.Select(f => f.FunctionName)});
            code_builder.AppendLine("std::map<std::string, std::map<std::string, std::function<malproxy::CallFuncResponse(const malproxy::CallFuncRequest&, FARPROC)>>> hooks = {");
            foreach (var module in hooks)
            {
                code_builder.AppendLine("{");
                code_builder.AppendLine($"\"{module.module}\",");
                code_builder.AppendLine("{");
                foreach (var func in module.funcs)
                    code_builder.AppendLine($"{{ \"{func}\", {func}_stub }},");
                code_builder.AppendLine("}");
                code_builder.AppendLine("},");
            }
            code_builder.AppendLine("};");
            return code_builder.ToString();
        }
        public void GenerateFieldCode(string output_path, IEnumerable<FunctionCodeGenerator> functions)
        {
            string code = GenerateFieldCode(functions);
            System.IO.File.WriteAllText(output_path, code);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            FunctionCodeGenerator CreateFileW = new FunctionCodeGenerator("Kernel32.dll", "CreateFileW", new HandleParameter());
            CreateFileW.AddParameter(new WstringParameter() { ParameterName = "lpFileName", Direction = ParameterDirection.in_param });
            CreateFileW.AddParameter(new UintParameter() { ParameterName = "dwDesiredAccess" });
            CreateFileW.AddParameter(new UintParameter() { ParameterName = "dwShareMode" });
            CreateFileW.AddParameter(new NullParameter() { ParameterName = "lpSecurityAttributes" });
            CreateFileW.AddParameter(new UintParameter() { ParameterName = "dwCreationDisposition" });
            CreateFileW.AddParameter(new UintParameter() { ParameterName = "dwFlagsAndAttributes" });
            CreateFileW.AddParameter(new HandleParameter() { ParameterName = "hTemplateFile" });

            FunctionCodeGenerator OutputDebugStringW = new FunctionCodeGenerator("Kernel32.dll", "OutputDebugStringW", null);
            OutputDebugStringW.AddParameter(new WstringParameter() { ParameterName = "lpOutputString", Direction = ParameterDirection.in_param });
            FunctionCodeGenerator OutputDebugStringA = new FunctionCodeGenerator("Kernel32.dll", "OutputDebugStringA", null);
            OutputDebugStringA.AddParameter(new StringParameter() { ParameterName = "lpOutputString", Direction = ParameterDirection.in_param });

            var NtQuerySystemInformation_buffer_param = new UserBufferParameter()
                { ParameterName = "SystemInformation", BufferSizeName = "SystemInformationLength", Direction = ParameterDirection.out_param};
            NtQuerySystemInformation_buffer_param.RelocationCodeGenerator = RelocateNtQuerySystemInformation;
            FunctionCodeGenerator NtQuerySystemInformation = new FunctionCodeGenerator("ntdll.dll", "NtQuerySystemInformation", new UintParameter());
            NtQuerySystemInformation.AddParameter(new UintParameter() { ParameterName = "SystemInformationClass" });
            NtQuerySystemInformation.AddParameter(NtQuerySystemInformation_buffer_param);
            NtQuerySystemInformation.AddParameter(new UintPtrParameter() {ParameterName = "ReturnLength", Direction = ParameterDirection.out_param});

            FunctionCodeGenerator GetProcessId = new FunctionCodeGenerator("Kernel32.dll", "GetProcessId", new UintParameter());
            GetProcessId.AddParameter(new HandleParameter() {ParameterName = "Process" });

            FunctionCodeGenerator OpenProcess = new FunctionCodeGenerator("Kernel32.dll", "OpenProcess", new HandleParameter());
            OpenProcess.AddParameter(new UintParameter() { ParameterName = "dwDesiredAccess" });
            OpenProcess.AddParameter(new BoolParameter() { ParameterName = "bInheritHandle" });
            OpenProcess.AddParameter(new UintParameter() { ParameterName = "dwProcessId" });

            FunctionCodeGenerator OpenProcessToken = new FunctionCodeGenerator("Advapi32.dll", "OpenProcessToken", new BoolParameter());
            OpenProcessToken.AddParameter(new HandleParameter() {ParameterName = "ProcessHandle" });
            OpenProcessToken.AddParameter(new UintParameter() { ParameterName = "DesiredAccess" });
            OpenProcessToken.AddParameter(new HandlePtrParameter() { ParameterName = "TokenHandle", Direction = ParameterDirection.out_param });

            FunctionCodeGenerator NtQueryInformationProcess = new FunctionCodeGenerator("ntdll.dll", "NtQueryInformationProcess", new UintParameter());
            NtQueryInformationProcess.AddParameter(new HandleParameter() { ParameterName = "ProcessHandle" });
            NtQueryInformationProcess.AddParameter(new UintParameter() { ParameterName = "PROCESSINFOCLASS" });
            NtQueryInformationProcess.AddParameter(new UserBufferParameter()
                { ParameterName = "ProcessInformation", BufferSizeName = "ProcessInformationLength", Direction = ParameterDirection.out_param });
            NtQueryInformationProcess.AddParameter(new UintPtrParameter() { ParameterName = "ReturnLength", Direction = ParameterDirection.out_param });

            FunctionCodeGenerator ReadProcessMemory = new FunctionCodeGenerator("Kernel32.dll", "ReadProcessMemory", new UintParameter());
            ReadProcessMemory.AddParameter(new HandleParameter() { ParameterName = "hProcess" });
            ReadProcessMemory.AddParameter(new VoidPtrParameter() { ParameterName = "lpBaseAddress" });
            ReadProcessMemory.AddParameter(new UserBufferParameter()
                { ParameterName = "lpBuffer", BufferSizeName = "nSize", Direction = ParameterDirection.out_param });
            ReadProcessMemory.AddParameter(new SizeTPtrParameter() { ParameterName = "lpNumberOfBytesRead", Direction = ParameterDirection.out_param });

            FunctionCodeGenerator FileTimeToLocalFileTime = new FunctionCodeGenerator("Kernel32.dll", "FileTimeToLocalFileTime", new BoolParameter());
            FileTimeToLocalFileTime.AddParameter(new FileTimePtrParameter() { ParameterName = "lpFileTime", Direction = ParameterDirection.in_param });
            FileTimeToLocalFileTime.AddParameter(new FileTimePtrParameter() { ParameterName = "lpLocalFileTime", Direction = ParameterDirection.out_param });

            FunctionCodeGenerator FileTimeToSystemTime = new FunctionCodeGenerator("Kernel32.dll", "FileTimeToSystemTime", new BoolParameter());
            FileTimeToSystemTime.AddParameter(new FileTimePtrParameter() { ParameterName = "lpFileTime", Direction = ParameterDirection.in_param });
            FileTimeToSystemTime.AddParameter(new FileTimePtrParameter() { ParameterName = "lpSystemTime", Direction = ParameterDirection.out_param });


            FunctionCodeGenerator RtlAdjustPrivilege = new FunctionCodeGenerator("ntdll.dll", "RtlAdjustPrivilege", new UintParameter());
            RtlAdjustPrivilege.AddParameter(new UintParameter() { ParameterName = "Privilege" });
            RtlAdjustPrivilege.AddParameter(new BoolParameter() { ParameterName = "Enable" });
            RtlAdjustPrivilege.AddParameter(new BoolParameter() { ParameterName = "CurrentThread" });
            RtlAdjustPrivilege.AddParameter(new BoolPtrParameter() { ParameterName = "Enabled", Direction = ParameterDirection.out_param });

            //FunctionCodeGenerator RtlEqualUnicodeString = new FunctionCodeGenerator("ntdll.dll", "RtlEqualUnicodeString", new BoolParameter());
            //RtlEqualUnicodeString.AddParameter(new PUNICODE_STRING() {ParameterName = "String1", Direction = ParameterDirection.in_param });
            //RtlEqualUnicodeString.AddParameter(new PUNICODE_STRING() {ParameterName = "String2", Direction = ParameterDirection.in_param });
            //RtlEqualUnicodeString.AddParameter(new BoolParameter() {ParameterName = "CaseInSensitive" });

            FunctionCodeGenerator GetLastError = new FunctionCodeGenerator("Kernel32.dll", "GetLastError", new UintParameter());

            CodeGenerator code = new CodeGenerator();
            string target = (args.Length > 0) ? args[0] : string.Empty;

            var functions = new[]
            {
                CreateFileW, OutputDebugStringW, OutputDebugStringA, GetLastError,
                NtQuerySystemInformation, OpenProcess, GetProcessId, OpenProcessToken, NtQueryInformationProcess, ReadProcessMemory, RtlAdjustPrivilege
            };

            code.GenerateHomeCode(System.IO.Path.Combine(target, "MalproxyClient", "autogenerated.home.cpp"), functions);
            code.GenerateFieldCode(System.IO.Path.Combine(target, "MalproxyServer", "autogenerated.field.cpp"), functions);
        }

        private static string RelocateNtQuerySystemInformation(string argument, string size, string relocations)
        {
            StringBuilder code = new StringBuilder();
            code.AppendLine($"if (NT_SUCCESS(raw_retval)) {{");
            code.AppendLine($" for (SYSTEM_PROCESS_INFORMATION* ptr = (SYSTEM_PROCESS_INFORMATION*){argument}; (char*)ptr < (char*){argument} + {size} && ptr->NextEntryOffset > 0; ptr = (SYSTEM_PROCESS_INFORMATION*)((char*)ptr + ptr->NextEntryOffset)) {{");
            code.AppendLine($"  {relocations}->add_offsets(((unsigned long long)(ULONG_PTR)&ptr->ImageName.Buffer) - (ULONG_PTR){argument});");
            code.AppendLine($" }}");
            code.AppendLine($"}}");
            return code.ToString();
        }
    }
}
