#pragma once

#include "malproxy.pb.h"
#include <string>
#include <functional>
#include <memory>
#include "FunctionDefs.h"


class RpcStub;

class RpcServerCallbacks
{
public:
	CallFuncCallback CallCollectorFunc;
	LoadLibraryCallback LoadLibraryFunc;
	LoadLibraryExCallback LoadLibraryExFunc;
	FreeLibraryCallback FreeLibraryFunc;
};