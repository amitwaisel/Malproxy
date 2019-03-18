// RemoteSyscallsClient.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#include <iostream>
#include "RpcLib/MalproxyServer.h"
#include "Framework/Event.h"
#include <Windows.h>
#include "Framework/Utils.h"

extern std::map<std::string, std::map<std::string, std::function<malproxy::CallFuncResponse(const malproxy::CallFuncRequest&, FARPROC)>>> hooks;

malproxy::CallFuncResponse CallCollectorFuncHandler(const malproxy::CallFuncRequest& request)
{
	HMODULE module = GetModuleHandleA(request.dll_name().c_str());
	FARPROC func = (FARPROC)GetProcAddress(module, request.function_name().c_str());

	auto func_ptr = hooks[request.dll_name()][request.function_name()];
	return func_ptr(request, func);
}

malproxy::LoadLibraryResponse LoadLibraryFuncHandler(const malproxy::LoadLibraryRequest& request)
{
	HMODULE module = LoadLibraryA(request.dll_name().c_str());
	malproxy::LoadLibraryResponse response;
	std::unique_ptr<malproxy::HandleType> handle = std::make_unique<malproxy::HandleType>();
	handle->set_handle((uint64_t)module);
	response.set_allocated_handle(handle.release());
	return response;
}

void FreeLibraryFuncHandler(const malproxy::FreeLibraryRequest& request)
{
	FreeLibrary((HMODULE)request.handle().handle());
}

std::shared_ptr<MalproxyServer> server_ptr = nullptr;

BOOL WINAPI consoleHandler(DWORD signal) {

	if (signal == CTRL_C_EVENT && server_ptr != nullptr)
		server_ptr->Shutdown();

	return TRUE;
}

int main()
{
	RpcServerCallbacks callbacks;
	callbacks.CallCollectorFunc = CallCollectorFuncHandler;
	callbacks.LoadLibraryFunc = LoadLibraryFuncHandler;
	callbacks.FreeLibraryFunc = FreeLibraryFuncHandler;

	if (!SetConsoleCtrlHandler(consoleHandler, TRUE))
	{
		return 1;
	}
	
	server_ptr = std::make_shared<MalproxyServer>(callbacks);
	server_ptr->Wait();
}
