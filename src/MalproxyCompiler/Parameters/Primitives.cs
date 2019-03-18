using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MalproxyCompiler.Parameters
{
    // A pointer parameter that must be NULL
    class NullParameter : BaseParameter
    {
        protected override string ParameterType => "LPVOID";
        public override string ParameterProtobufName => "empty"; // bool empty_val = 15;
        protected override string DefaultValue => "true";

        public override string GetFieldInputCode(string request_name, int index)
        {
            // LPSECURITY_ATTRIBUTES lpSecurityAttributes = (LPSECURITY_ATTRIBUTES)nullptr;
            return $"{ParameterType} {ParameterName} = ({ParameterType})nullptr;";
        }
    }

    class VoidPtrParameter : BaseParameter
    {
        protected override string ParameterType => "LPVOID";
        public override string ParameterProtobufName => "uint64";
        public override string ParameterTypeProtobufCast => "uint64_t";
    }
    
    class UintParameter : BaseParameter
    {
        protected override string ParameterType => "DWORD";
        public override string ParameterProtobufName => "uint32";
    }

    class UintPtrParameter : PointerParameter
    {
        protected override string ParameterType => "DWORD";
        public override string ParameterProtobufName => "uint32";
    }
    
    class Uint64Parameter : BaseParameter
    {
        protected override string ParameterType => "DWORD64";
        public override string ParameterProtobufName => "uint64";
    }

    class Uint64PtrParameter : PointerParameter
    {
        protected override string ParameterType => "DWORD64";
        public override string ParameterProtobufName => "uint64";
    }

    class BoolParameter : BaseParameter
    {
        protected override string ParameterType => "BOOL";
        public override string ParameterProtobufName => "bool";
    }

    class BoolPtrParameter : PointerParameter
    {
        protected override string ParameterType => "BOOL";
        public override string ParameterProtobufName => "bool";
    }
}
