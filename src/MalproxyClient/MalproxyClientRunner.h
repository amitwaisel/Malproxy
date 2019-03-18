#pragma once
#include <vector>
#include <memory>
#include "RpcLib/MalproxySession.h"
#include "MemoryModule.h"

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

	void RunRemote(const std::string& module_path);
	std::shared_ptr<MalproxySession> Session() { return _client; }

private:
	Buffer MalproxyReadFile(const std::string& path);

	static MalproxyClientRunner& InstanceImpl(const std::shared_ptr<MalproxySession>& client = nullptr);

	static HCUSTOMMODULE MalproxyLoadLibrary(const char* dll_name, void* context);
	static FARPROC MalproxyGetProcAddress(HCUSTOMMODULE library, LPCSTR function_name, void *context);
	static void MalproxyClientRunner::MalproxyFreeLibrary(HCUSTOMMODULE module, void *context);

private:
	static std::shared_ptr<MalproxyClientRunner> _instance;

private:
	std::shared_ptr<MalproxySession> _client;
	std::map<HCUSTOMMODULE, std::string> _loaded_modules;
	std::map<std::string, std::map<std::string, FARPROC>> _hooks;

};