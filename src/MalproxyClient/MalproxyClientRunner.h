#pragma once
#include <vector>
#include <memory>
#include "RpcLib/MalproxySession.h"
#include "Framework/MemoryModule.h"

using Buffer = std::vector<unsigned char>;

class MalproxyClientRunner
{
private:
	MalproxyClientRunner(const std::shared_ptr<MalproxySession>& client = nullptr);
	virtual ~MalproxyClientRunner() = default;

	MalproxyClientRunner(MalproxyClientRunner const&) = delete;
	void operator=(MalproxyClientRunner const&) = delete;

public:
	static MalproxyClientRunner& Instance();
	static void Init(const std::shared_ptr<MalproxySession>& client);

	void RunRemote(const std::wstring& module_path, const std::wstring& pwd, const std::wstring& arguments);
	std::shared_ptr<MalproxySession> Session() { return _client; }

	std::wstring& GetFakeCommandLineW() { return _command_line; }
	std::wstring& GetFakeModuleNameW() { return _module_name; }
	std::string& GetFakeCommandLineA() { return _command_line_ascii; }
	std::string& GetFakeModuleNameA() { return _module_name_ascii; }

private:
	static Buffer MalproxyReadFile(const std::wstring& path);
	void HookPayloadCommandLine(const std::wstring& module_path, const std::wstring& pwd, const std::wstring& arguments);

	static MalproxyClientRunner& InstanceImpl(const std::shared_ptr<MalproxySession>& client = nullptr);

	static HCUSTOMMODULE MalproxyLoadLibrary(const char* dll_name, void* context);
	static FARPROC MalproxyGetProcAddress(HCUSTOMMODULE library, LPCSTR function_name, void *context);
	static void MalproxyFreeLibrary(HCUSTOMMODULE module, void *context);


private:
	static std::shared_ptr<MalproxyClientRunner> _instance;

private:
	std::shared_ptr<MalproxySession> _client;
	//std::map<HMODULE, std::string> _local_loaded_modules;
	std::map<HCUSTOMMODULE, std::string> _loaded_modules;
	std::map<std::string, std::map<std::string, FARPROC>> _hooks;
	std::wstring _command_line;
	std::wstring _module_name;
	std::string _command_line_ascii;
	std::string _module_name_ascii;
};