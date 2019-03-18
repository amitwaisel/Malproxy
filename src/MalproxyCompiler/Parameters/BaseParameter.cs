using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MalproxyCompiler.Parameters
{
    abstract class BaseParameter
    {
        public string ParameterName { get; set; }
        protected abstract string ParameterType { get; }
        public abstract string ParameterProtobufName { get; }
        protected virtual string DefaultValue => ParameterName;
        public bool HasType => (!string.IsNullOrEmpty(ParameterType));

        public virtual string ParameterPrototypeTypeAndName => $"{ParameterType} {ParameterName}";
        public virtual string ParameterTypeCast => ParameterType;
        public virtual string ParameterTypeProtobufCast => ParameterType;
        public virtual string ParameterPrototypeName => ParameterName;
        public virtual string ParameterInFunctionCall => ParameterName;

        public virtual string GetValueCode(string argname)
        {
            // {response}.return_value().int32_val();
            return $"({ParameterTypeCast}){argname}.{ParameterProtobufName}_val();";
        }
        public virtual string GetReturnValueCode(string argname)
        {
            // {response}.return_value().int32_val();
            return $"return ({ParameterTypeCast}){argname}.return_value().{ParameterProtobufName}_val();";
        }
        public virtual string SetValueCode(string argname_ptr, string raw_value_name)
        {
            // {response}.set_int32_val({raw});
            return $"{argname_ptr}->set_{ParameterProtobufName}_val(({ParameterTypeProtobufCast}){raw_value_name});";
        }

        protected virtual string getHomeInDeclaration(string request_name)
        {
            // malproxy::Argument* arg_dwDesiredAccess = request.add_in_arguments();

            return $"malproxy::Argument* arg_{ParameterName} = {request_name}.add_in_arguments();";
        }

        // The input parameter on the attacker side
        public virtual string GetHomeInputCode(string request_name)
        {
            // malproxy::Argument* arg_dwDesiredAccess = request.add_in_arguments();
            // arg_dwDesiredAccess->set_uint32_val(dwDesiredAccess);

            return $@"{getHomeInDeclaration(request_name)}
arg_{ParameterName}->set_{ParameterProtobufName}_val(({ParameterTypeProtobufCast}){DefaultValue});";
        }

        // The input parameter on the victim side
        public virtual string GetFieldInputCode(string request_name, int index)
        {
            // DWORD dwDesiredAccess = (DWORD)request.in_arguments(1).uint32_val();
            return $"{ParameterTypeCast} {ParameterName} = ({ParameterTypeCast}){request_name}.in_arguments({index}).{ParameterProtobufName}_val();";
        }
    }
}
