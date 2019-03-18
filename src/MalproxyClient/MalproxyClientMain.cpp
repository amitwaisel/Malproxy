#include <cstdio>
#include "RpcLib/MalproxySession.h"
#include "Framework/Utils.h"
#include "MalproxyClientRunner.h"

enum args
{
	ARGV_SELF = 0,
	ARGV_PAYLOAD,
	ARGV_MALPROXY_SERVER_ADDRESS,
	ARGV_MALPROXY_SERVER_PORT,
	ARGV_ARGC
};

int usage(char* argv[])
{
	printf("Usage: %s <payload path> <server address> <server port>\n", argv[ARGV_SELF]);
	return -1;
}

int main(int argc, char* argv[])
{
	if (argc != ARGV_ARGC)
		return usage(argv);

	std::string url = StringUtils::FormatString("%s:%s", argv[ARGV_MALPROXY_SERVER_ADDRESS], argv[ARGV_MALPROXY_SERVER_PORT]);
	std::shared_ptr<MalproxySession> client = std::make_shared<MalproxySession>();
	client->Connect(url, grpc::InsecureChannelCredentials());

	MalproxyClientRunner::Init(client);
	MalproxyClientRunner::Instance().RunRemote(argv[ARGV_PAYLOAD]);
}
